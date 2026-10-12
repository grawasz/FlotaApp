// Pomiar wydajności wersji przeglądarkowej na danych symulatora UniAVL (docs/WEB.md, T9.3a).
// Każdy przebieg w czystym profilu przeglądarki (pusta pamięć podręczna), następnie ponowne otwarcie strony.
// Użycie: node tools/TestyUi/pomiar.mjs [adres] [liczba przebiegów] [plik wyników JSON]
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { Browser, UnoApp, sleep } from './cdp.mjs';

const baseUrl = process.argv[2] ?? 'http://localhost:5294/';
// Parametr ?pomiar=1 włącza znaczniki „[pomiar] nazwa” w konsoli aplikacji (Flota.App/Services/Pomiar.cs)
const url = baseUrl + (baseUrl.includes('?') ? '&' : '?') + 'pomiar=1'; // parametr bez wartości blokuje uruchomienie aplikacji (docs/WEB.md)
const runs = Number(process.argv[3] ?? 3);
const out = process.argv[4];
const objectName = 'Renault Trafic';
// FLOTA_POMIAR_ZRZUTY=<katalog>: zrzuty ekranu po każdym kroku (diagnostyka)
const shots = process.env.FLOTA_POMIAR_ZRZUTY;
if (shots) mkdirSync(shots, { recursive: true });
const shot = async (b, name) => { if (shots) await b.screenshot(join(shots, `${name}.png`)); };
const datePattern = /^\d{4}-\d{2}-\d{2} \d{2}:\d{2}/;

/** Rejestr żądań sieciowych z czasem zegara ściennego (ms). */
class NetLog {
  constructor(browser) {
    this.requests = new Map();
    this.offset = null;
    browser.on((method, p) => {
      if (method === 'Network.requestWillBeSent') {
        if (this.offset === null) this.offset = p.wallTime * 1000 - p.timestamp * 1000;
        this.requests.set(p.requestId, { url: p.request.url, method: p.request.method, sent: this.wall(p.timestamp), bytes: 0 });
      } else if (method === 'Network.responseReceived') {
        const r = this.requests.get(p.requestId); if (r) { r.status = p.response.status; r.fromCache = p.response.fromDiskCache || p.response.fromServiceWorker; }
      } else if (method === 'Network.loadingFinished') {
        const r = this.requests.get(p.requestId); if (r) { r.finished = this.wall(p.timestamp); r.bytes = p.encodedDataLength; }
      }
    });
  }
  wall(ts) { return ts * 1000 + (this.offset ?? 0); }
  clear() { this.requests.clear(); }
  find(predicate) { return [...this.requests.values()].filter(predicate); }
  first(method, path) { return this.find(r => r.method === method && new URL(r.url).pathname.startsWith(path)).sort((a, b) => a.sent - b.sent)[0]; }
  bytes() { return this.find(() => true).reduce((s, r) => s + (r.bytes || 0), 0); }
}

/** Oczekiwanie na węzeł semantyczny; odpytywanie co 100 ms (wywołanie czeka na wolny wątek strony). */
async function waitNode(app, predicate, timeoutMs = 120000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const n = (await app.nodes()).find(predicate);
    if (n) return { node: n, at: Date.now() };
    await sleep(100);
  }
  return null;
}

async function waitLoaded(browser, start, timeoutMs = 120000) {
  while (Date.now() - start < timeoutMs) {
    // Ekran ładowania usunięty albo ukryty (po ponownym otwarciu strony pozostaje w dokumencie)
    if (await browser.eval(`(() => { const l = document.querySelector('.uno-loader');
        return !!document.getElementById('uno-canvas') && (!l || getComputedStyle(l).display === 'none' || getComputedStyle(l).opacity === '0'); })()`)) return Date.now();
    await sleep(100);
  }
  throw new Error('Aplikacja nie uruchomiła się.');
}

async function run(index) {
  const browser = await Browser.launch({ width: 1400, height: 900, port: 9450 + index });
  const net = new NetLog(browser);
  await browser.send('Network.enable');
  // Okno „Opuścić stronę?” po zalogowaniu blokowałoby ponowne otwarcie strony
  await browser.send('Page.enable');
  browser.on((method) => { if (method === 'Page.javascriptDialogOpening') browser.send('Page.handleJavaScriptDialog', { accept: true }); });
  // Znaczniki aplikacji z czasem zdarzenia konsoli (ms, zegar ścienny)
  const marks = [];
  await browser.send('Runtime.enable');
  browser.on((method, p) => {
    const text = method === 'Runtime.consoleAPICalled' ? p.args?.[0]?.value : null;
    if (typeof text === 'string' && text.startsWith('[pomiar] ')) marks.push({ name: text.slice(9).trim(), at: p.timestamp });
  });
  const r = { run: index };
  try {
    const app = new UnoApp(browser);

    // 1. Pierwsze otwarcie (pusta pamięć podręczna): do usunięcia ekranu ładowania
    const navStart = Date.now();
    await browser.goto(url);
    r.coldLoadMs = await waitLoaded(browser, navStart) - navStart;
    r.coldBytes = net.bytes();
    r.coldRequests = net.requests.size;
    r.coldMarks = Object.fromEntries(marks.map(m => [m.name, Math.round(m.at - navStart)]));

    // 2. Logowanie: od odpowiedzi POST /auth/login do żądania i odpowiedzi /fleet/snapshot oraz obiektów na liście
    await app.open(url); // włączenie warstwy semantycznej (strona już załadowana)
    net.clear();
    await app.focusInput('Użytkownik'); await browser.type('demo');
    await app.focusInput('Hasło'); await browser.type('demo');
    await browser.key('Enter');
    const listed = await waitNode(app, n => n.text.trim() === objectName);
    const login = net.first('POST', '/auth/login');
    const snapshot = net.first('GET', '/fleet/snapshot');
    r.loginResponseMs = login?.finished && login.finished - login.sent;
    r.loginToSnapshotRequestMs = snapshot && login?.finished ? Math.round(snapshot.sent - login.finished) : null;
    r.snapshotResponseMs = snapshot?.finished ? Math.round(snapshot.finished - snapshot.sent) : null;
    r.loginToObjectsListedMs = listed && login?.finished ? Math.round(listed.at - login.finished) : null;
    const loginMarks = () => {
      // Znaczniki po zalogowaniu względem odpowiedzi POST /auth/login (pierwsze wystąpienie)
      r.loginMarks = {};
      for (const m of marks) if (login?.finished && m.at >= login.sent && !(m.name in r.loginMarks)) r.loginMarks[m.name] = Math.round(m.at - login.finished);
    };

    await shot(browser, `${index}-1-lista`);
    // 3. Raport trasy: wybór obiektu, otwarcie sekcji, „Generuj raport (trasa)” do wyświetlenia kursów
    await sleep(2000);
    await app.clickNode(listed.node);
    await sleep(1000);
    const sectionButton = (await app.nodes()).find(n => n.tag === 'BUTTON' && n.label === 'Raport trasy' && n.y < 44);
    const openStart = Date.now();
    await app.clickNode(sectionButton);
    const generate = await waitNode(app, n => n.tag === 'BUTTON' && n.label === 'Generuj raport (trasa)');
    r.routePanelOpenMs = generate.at - openStart;
    const panelMarks = marks.filter(m => m.name.startsWith('panel.route.'));
    if (panelMarks.length === 2) r.routePanelCreateMs = Math.round(panelMarks[1].at - panelMarks[0].at);
    await shot(browser, `${index}-2-raport`);
    await sleep(1500);
    net.clear();
    const reportStart = Date.now();
    // Przycisk wywoływany przez element semantyczny (współrzędne węzłów w panelach bywają względne)
    await browser.eval(`document.querySelector('#uno-semantics-root [aria-label="Generuj raport (trasa)"]').click(); true`);
    // Zakończenie: odpowiedzi API raportu odebrane, brak nowych żądań przez 1,5 s, wątek strony wolny
    // (komórki tabel nie występują w warstwie semantycznej)
    // Po odebraniu odpowiedzi: wywołanie czeka na wolny wątek strony (przetworzenie danych i budowa widoku);
    // następnie 1,5 s bez nowych żądań API kończy pomiar
    let doneAt = null;
    const apiCalls = () => net.find(x => new URL(x.url).pathname.startsWith('/objects/'));
    while (Date.now() - reportStart < 60000) {
      const api = apiCalls();
      if (api.length > 0 && api.every(x => x.finished)) {
        const count = api.length;
        await browser.eval('1');
        doneAt = Date.now();
        await sleep(1500);
        if (apiCalls().length === count) break;
      }
      await sleep(100);
    }
    const route = net.first('GET', '/objects/');
    r.routeApiMs = route?.finished ? Math.round(route.finished - route.sent) : null;
    r.routeRequests = net.find(x => new URL(x.url).pathname.startsWith('/objects/')).map(x => new URL(x.url).pathname.split('/').pop());
    r.routeReportMs = doneAt ? doneAt - reportStart : null;
    await shot(browser, `${index}-3-kursy`);

    loginMarks();
    // 4. Ponowne otwarcie strony (pamięć podręczna przeglądarki)
    net.clear();
    const warmStart = Date.now();
    await browser.goto(url);
    r.warmLoadMs = await waitLoaded(browser, warmStart) - warmStart;
    r.warmBytes = net.bytes();
  } catch (e) {
    r.error = e.message;
    try { await shot(browser, `${index}-blad`); } catch { }
  } finally {
    await browser.close();
  }
  return r;
}

const results = [];
for (let i = 1; i <= runs; i++) {
  const r = await run(i);
  console.log(JSON.stringify(r));
  results.push(r);
}

const keys = ['coldLoadMs', 'warmLoadMs', 'loginToSnapshotRequestMs', 'snapshotResponseMs', 'loginToObjectsListedMs', 'routePanelOpenMs', 'routeApiMs', 'routeReportMs'];
const median = values => { const v = values.filter(x => typeof x === 'number').sort((a, b) => a - b); return v.length ? v[Math.floor((v.length - 1) / 2)] : null; };
const summary = Object.fromEntries(keys.map(k => [k, { median: median(results.map(r => r[k])), min: Math.min(...results.map(r => r[k] ?? Infinity)), max: Math.max(...results.map(r => r[k] ?? -Infinity)) }]));
summary.coldKB = Math.round(median(results.map(r => r.coldBytes)) / 1024);
console.log('\nMediana (min–max) [ms]:');
for (const k of keys) console.log(`  ${k.padEnd(26)} ${summary[k].median} (${summary[k].min}–${summary[k].max})`);
console.log(`  przesłane dane (pierwsze otwarcie): ${summary.coldKB} KB`);
if (out) writeFileSync(out, JSON.stringify({ url, runs: results, summary }, null, 2));
