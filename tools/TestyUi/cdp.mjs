// Sterowanie przeglądarką (Edge/Chrome bez interfejsu) przez Chrome DevTools Protocol — bez zależności zewnętrznych (Node 22+).
import { spawn } from 'node:child_process';
import { existsSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const candidates = [
  process.env.BROWSER,
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  '/usr/bin/microsoft-edge', '/usr/bin/google-chrome', '/usr/bin/chromium',
].filter(Boolean);

export const sleep = ms => new Promise(r => setTimeout(r, ms));

export class Browser {
  static async launch({ width = 1400, height = 900, port = 9444 } = {}) {
    const exe = candidates.find(p => existsSync(p));
    if (!exe) throw new Error('Brak przeglądarki Edge/Chrome (zmienna BROWSER).');
    const profile = mkdtempSync(join(tmpdir(), 'flota-ui-'));
    // Bez wstrzymywania klatek i zegarów strony w tle: potwierdzenia zdarzeń wejścia czekają na klatkę
    const proc = spawn(exe, ['--headless=new', `--remote-debugging-port=${port}`, '--no-first-run', '--no-default-browser-check',
      '--disable-background-timer-throttling', '--disable-renderer-backgrounding', '--disable-backgrounding-occluded-windows',
      // WebGL bez procesora graficznego (maszyny CI): programowy SwiftShader — renderer Skia aplikacji Uno wymaga WebGL
      '--enable-unsafe-swiftshader',
      `--user-data-dir=${profile}`, `--window-size=${width},${height}`, '--force-device-scale-factor=1', 'about:blank'], { stdio: 'ignore' });
    let page;
    for (let i = 0; i < 100 && !page; i++) {
      try { page = (await (await fetch(`http://127.0.0.1:${port}/json/list`)).json()).find(t => t.type === 'page'); } catch { }
      if (!page) await sleep(200);
    }
    if (!page) throw new Error('Przeglądarka nie udostępniła protokołu CDP.');
    const b = new Browser(proc, profile, new WebSocket(page.webSocketDebuggerUrl));
    await new Promise(r => b.ws.addEventListener('open', r));
    await b.resize(width, height);
    return b;
  }

  constructor(proc, profile, ws) {
    this.proc = proc; this.profile = profile; this.ws = ws; this.id = 0; this.pending = new Map(); this.listeners = [];
    ws.addEventListener('message', e => {
      const m = JSON.parse(e.data);
      if (m.id && this.pending.has(m.id)) { this.pending.get(m.id)(m); this.pending.delete(m.id); }
      else if (m.method) for (const l of this.listeners) l(m.method, m.params);
    });
  }

  /** Zdarzenia CDP (np. Network.requestWillBeSent po włączeniu domeny Network). */
  on(listener) { this.listeners.push(listener); }

  /** Wywołanie CDP; brak odpowiedzi w limicie czasu (np. zawieszona strona) — błąd zamiast oczekiwania bez końca. */
  send(method, params = {}, timeoutMs = 60000) {
    return new Promise((resolve, reject) => {
      const i = ++this.id;
      const timer = setTimeout(() => { this.pending.delete(i); reject(new Error(`CDP ${method}: brak odpowiedzi w ${timeoutMs} ms (strona nie odpowiada)`)); }, timeoutMs);
      const start = Date.now();
      this.pending.set(i, m => {
        clearTimeout(timer);
        // FLOTA_UI_DEBUG=1: wywołania dłuższe niż 2 s w dzienniku
        if (process.env.FLOTA_UI_DEBUG && Date.now() - start > 2000) console.error(`[cdp] ${method} ${JSON.stringify(params).slice(0, 120)} — ${Date.now() - start} ms`);
        resolve(m);
      });
      if (process.env.FLOTA_UI_DEBUG && method.startsWith('Input.')) console.error(`[cdp] → ${method} ${JSON.stringify(params).slice(0, 120)}`);
      this.ws.send(JSON.stringify({ id: i, method, params }));
    });
  }

  async resize(width, height) {
    this.width = width; this.height = height;
    await this.send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: false });
  }

  async goto(url) { await this.send('Page.navigate', { url }); }

  async eval(expression) {
    const r = await this.send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (r.result?.exceptionDetails) throw new Error(r.result.exceptionDetails.text);
    return r.result?.result?.value;
  }

  async click(x, y) {
    for (const type of ['mouseMoved', 'mousePressed', 'mouseReleased'])
      await this.send('Input.dispatchMouseEvent', { type, x, y, button: 'left', clickCount: 1, buttons: type === 'mousePressed' ? 1 : 0 });
    await sleep(200);
  }

  /**
   * Zdarzenie kółka. Przeglądarka bez interfejsu potrafi nie potwierdzić zdarzenia kółka, choć strona działa
   * (sprawdzone: JavaScript odpowiada w 1–2 ms) — potwierdzenie oczekiwane najwyżej 3 s.
   */
  async wheel(x, y, deltaX, deltaY = 0) {
    const ack = this.send('Input.dispatchMouseEvent', { type: 'mouseWheel', x, y, deltaX, deltaY }, 600000).catch(() => { });
    await Promise.race([ack, sleep(3000)]);
    await sleep(200);
  }

  async type(text) { await this.send('Input.insertText', { text }); await sleep(100); }

  async key(key, code = 13) {
    for (const type of ['rawKeyDown', 'keyUp']) await this.send('Input.dispatchKeyEvent', { type, key, code: key, windowsVirtualKeyCode: code });
    await sleep(100);
  }

  async screenshot(path) {
    const r = await this.send('Page.captureScreenshot', { format: 'png' });
    writeFileSync(path, Buffer.from(r.result.data, 'base64'));
  }

  async close() {
    try { this.ws.close(); } catch { }
    this.proc.kill();
    await sleep(500);
    try { rmSync(this.profile, { recursive: true, force: true }); } catch { }
  }
}

/**
 * Aplikacja Uno (WebAssembly, renderer Skia): elementy odnajdywane w warstwie semantycznej
 * (#uno-semantics-root), którą Uno tworzy dla czytników ekranu po włączeniu dostępności.
 */
export class UnoApp {
  constructor(browser) { this.b = browser; }

  /** Otwarcie aplikacji i włączenie warstwy semantycznej (ponawiane do czasu uruchomienia aplikacji). */
  async open(url, timeoutMs = 90000) {
    await this.b.goto(url);
    const start = Date.now();
    // Uruchomienie: ekran ładowania usunięty, płótno Skia obecne
    while (Date.now() - start < timeoutMs
           && !await this.b.eval("!document.querySelector('.uno-loader') && !!document.getElementById('uno-canvas') && !!document.getElementById('uno-enable-accessibility')"))
      await sleep(500);
    await sleep(2000);
    if (!await this.b.eval("!!document.getElementById('uno-enable-accessibility')")) {
      const state = await this.b.eval(`JSON.stringify({ loader: !!document.querySelector('.uno-loader'), canvas: !!document.getElementById('uno-canvas'),
        webgl2: !!document.createElement('canvas').getContext('webgl2'), webgl: !!document.createElement('canvas').getContext('webgl') })`);
      throw new Error(`Aplikacja nie uruchomiła się w ${timeoutMs} ms (${state}).`);
    }
    // Po przeładowaniu strony warstwa może być już włączona — ponowne kliknięcie by ją wyłączyło
    if (await this.b.eval("document.querySelectorAll('#uno-semantics-root *').length") > 0) return;
    await this.b.eval("document.getElementById('uno-enable-accessibility').click(); true");
    while (Date.now() - start < timeoutMs) {
      if (await this.b.eval("document.querySelectorAll('#uno-semantics-root *').length") > 0) return;
      await sleep(500);
    }
    throw new Error('Aplikacja nie uruchomiła się (brak warstwy semantycznej).');
  }

  /** Węzły semantyczne: rola, etykieta, tekst, prostokąt. */
  async nodes() {
    return this.b.eval(`[...document.querySelectorAll('#uno-semantics-root *')].map(e => {
      const r = e.getBoundingClientRect();
      return { tag: e.tagName, role: e.getAttribute('role') || '', label: e.getAttribute('aria-label') || '',
               labelledBy: e.getAttribute('aria-labelledby') || '', text: (e.childElementCount === 0 ? e.textContent : '') || '',
               inScrollBar: !!e.parentElement?.closest('[role=scrollbar]'), inButton: !!e.parentElement?.closest('button,[role=button]'),
               html: e.outerHTML.slice(0, 160),
               x: r.x, y: r.y, w: r.width, h: r.height };
    })`);
  }

  async waitFor(predicate, timeoutMs = 30000) {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      const found = (await this.nodes()).find(predicate);
      if (found) return found;
      await sleep(500);
    }
    return null;
  }

  /**
   * Oczekiwanie na wolny wątek strony: zadanie z kolejki strony (setTimeout) wykonane dwukrotnie z przerwą
   * (requestAnimationFrame w przeglądarce bez interfejsu nie jest wywoływane niezawodnie).
   */
  async idle() {
    for (let i = 0; i < 2; i++) {
      await this.b.eval('new Promise(r => setTimeout(() => r(true), 0))');
      await sleep(300);
    }
  }

  /** Kliknięcie w środek węzła; kursor odsuwany potem na krawędź okna (bez podpowiedzi nad treścią). */
  async clickNode(n) {
    await this.b.click(n.x + n.w / 2, n.y + n.h / 2);
    await this.b.send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: this.b.width - 2, y: this.b.height - 2 });
  }

  /** Fokus na polu tekstowym o danej nazwie dostępnej (pole semantyczne przekazuje wprowadzany tekst do kontrolki). */
  async focusInput(label) {
    if (!await this.waitFor(n => n.tag === 'INPUT' && n.label === label)) throw new Error(`Brak pola „${label}”.`);
    await this.b.eval(`document.querySelector('#uno-semantics-root input[aria-label="${label}"]').focus(); true`);
  }

  async login(user, password) {
    await this.focusInput('Użytkownik');
    await this.b.type(user);
    await this.focusInput('Hasło');
    await this.b.type(password);
    await this.b.key('Enter');
    if (!await this.waitFor(n => n.label === 'Menu', 60000)) throw new Error('Logowanie nie powiodło się.');
  }
}
