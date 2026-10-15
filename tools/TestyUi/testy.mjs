// Testy interfejsu wersji przeglądarkowej na danych symulatora UniAVL (docs/TESTY-UI.md).
//   T1.7 — przejście do każdej sekcji przy szerokości 400 px i 1400 px
//   T7.4 — nazwa dostępna (aria-label) każdej kontrolki interaktywnej we wszystkich sekcjach
//   T9.2 — wznowienie sesji po przeładowaniu strony, wylogowanie
//   T9.5 — odnośnik do pakietu Android wyłącznie, gdy Flota.Api go udostępnia (ekran logowania i menu boczne)
// Użycie: node tools/TestyUi/testy.mjs [adres] [katalog zrzutów]
import { mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { Browser, UnoApp, sleep } from './cdp.mjs';

const url = process.argv[2] ?? 'http://localhost:5294/';
const shots = process.argv[3];
if (shots) mkdirSync(shots, { recursive: true });

const sections = ['Obiekty', 'Szczegóły obiektu', 'Mapa', 'Obszary', 'Kierowcy', 'Zdarzenia', 'Wiadomości',
  'Raport trasy', 'Raport floty', 'Ranking Eco-Driving', 'Karty drogowe', 'Wyposażenie'];
const toolbarHeight = 44;
const interactiveRoles = new Set(['button', 'checkbox', 'combobox', 'textbox', 'slider', 'spinbutton', 'radio', 'switch', 'tab', 'menuitem', 'option']);
const failures = [];
const check = (ok, message) => { console.log(`${ok ? 'OK  ' : 'BŁĄD'} ${message}`); if (!ok) failures.push(message); };

/** Kontrolka interaktywna widoczna dla czytnika ekranu (bez elementów pasków przewijania i elementów ukrytych). */
// Elementy pasków przewijania (strzałki, suwak ≤ 16 px) Uno wystawia jako przyciski bez nazwy — poza zakresem aplikacji
const isInteractive = n => (interactiveRoles.has(n.role) || ['BUTTON', 'INPUT'].includes(n.tag)) && !n.inScrollBar && n.w > 16 && n.h > 16;
const unnamedIn = nodes => nodes.filter(n => isInteractive(n) && !n.label && !n.labelledBy);

const isToolbarButton = (n, name) => n.tag === 'BUTTON' && n.label === name && n.y < toolbarHeight;
/** Nagłówek panelu: tekst poza przyciskami (współrzędne węzłów w układzie wąskim bywają względne). */
const isHeader = (n, name) => n.text.trim() === name && n.tag !== 'BUTTON' && !n.inButton;
const panelHeader = (nodes, name) => nodes.find(n => isHeader(n, name));

/**
 * Położenie paska po ustaniu ruchu: dwa kolejne odczyty (co 600 ms) bez zmiany położenia przycisku.
 * Zdarzenia kółka wysłane, gdy wątek strony jest zajęty (np. budowa nowo utworzonego panelu), bywają przetwarzane
 * z opóźnieniem — odczyt wcześniejszy dawałby nieaktualne współrzędne.
 */
async function settledNodes(app, name, timeoutMs = 15000) {
  const start = Date.now();
  let previous = null;
  for (;;) {
    const nodes = await app.nodes();
    const n = nodes.find(x => isToolbarButton(x, name));
    if (previous !== null && n?.x === previous || Date.now() - start > timeoutMs) return { nodes, n };
    previous = n?.x;
    await sleep(600);
  }
}

/**
 * Przycisk sekcji na pasku, w całości w widocznej części paska (prawą część zajmują wskaźnik połączenia i menu);
 * w razie potrzeby pasek przewijany w poziomie.
 */
async function toolbarButton(app, name) {
  let previous;
  for (let i = 0; i < 16; i++) {
    // Kliknięcie wysłane, gdy wątek strony kończy układ po poprzedniej akcji, bywa pomijane
    await app.idle();
    const { nodes, n } = await settledNodes(app, name);
    const menu = nodes.find(x => x.tag === 'BUTTON' && x.label === 'Menu' && x.y < toolbarHeight);
    const limit = (menu?.x ?? app.b.width) - (app.b.width >= 600 ? 130 : 60);
    if (n && n.x >= 0 && n.x + n.w <= limit) return n;
    // Koniec paska: przewijanie nie zmienia już położenia przycisku
    if (n && previous && n.x === previous.x) return n;
    previous = n;
    await app.b.wheel(Math.min(limit, app.b.width) / 2, toolbarHeight / 2, n && n.x < 0 ? -120 : 120);
  }
  return null;
}

/**
 * Przełączenie sekcji: przycisk przewinięty do widocznej części paska (sprawdzenie dostępności na wąskim ekranie),
 * kliknięcie przez element semantyczny przycisku — współrzędne węzłów paska po przewinięciu bywają nieaktualne.
 */
async function toggleSection(app, button) {
  if (!button) return false;
  await app.b.eval(`(() => { const b = [...document.querySelectorAll('#uno-semantics-root button')]
      .find(e => e.getAttribute('aria-label') === ${JSON.stringify(button.label)} && e.getBoundingClientRect().y < ${toolbarHeight});
    b?.click(); return !!b; })()`);
  return true;
}

async function sectionsAt(width, height) {
  const browser = await Browser.launch({ width, height });
  // Błędy konsoli strony (diagnostyka niepowodzeń)
  const consoleErrors = [];
  await browser.send('Runtime.enable');
  browser.on((method, p) => {
    if (method === 'Runtime.exceptionThrown') consoleErrors.push(p.exceptionDetails?.exception?.description ?? p.exceptionDetails?.text);
    else if (method === 'Runtime.consoleAPICalled' && ['error', 'warning'].includes(p.type))
      consoleErrors.push((p.args ?? []).map(a => a.value ?? a.description).join(' '));
  });
  try {
    const app = new UnoApp(browser);
    await app.open(url);
    await app.login('demo', 'demo');
    await sleep(3000);
    for (const name of sections) {
      // Sekcja widoczna domyślnie albo otwierana przyciskiem paska i zamykana po sprawdzeniu
      const shown = !!await app.waitFor(n => isHeader(n, name), 2500);
      if (!shown) {
        const button = await toolbarButton(app, name);
        if (!button) { check(false, `${width} px: brak przycisku sekcji „${name}”`); continue; }
        await toggleSection(app, button);
        await sleep(1500);
      }
      // Panel tworzony przy pierwszym wyświetleniu (ShellPage.EnsurePanel) — dłuższy czas na układ wąski
      const header = await app.waitFor(n => isHeader(n, name), 15000);
      check(!!header, `${width} px: sekcja „${name}” wyświetlona`);
      if (!header) {
        // Zrzut stanu przy niepowodzeniu (diagnostyka)
        const failShot = join(tmpdir(), `flota-ui-blad-${width}-${sections.indexOf(name) + 1}.png`);
        await browser.screenshot(failShot);
        console.log(`     zrzut: ${failShot}`);
        const nodes = await app.nodes();
        console.log(`     włączone: ${nodes.filter(n => n.tag === 'BUTTON' && n.y < toolbarHeight && /tabindex="0"/.test(n.html)).map(n => n.label).join(', ')}`);
        console.log(`     nagłówki: ${sections.filter(s => nodes.some(n => isHeader(n, s))).join(', ')}`);
        for (const e of consoleErrors.slice(-8)) console.log(`     konsola: ${String(e).slice(0, 400).replace(/\s+/g, ' ')}`);
      }
      if (shots) await browser.screenshot(join(shots, `sekcja-${width}-${sections.indexOf(name) + 1}.png`));
      if (!shown) {
        // Zamknięcie potwierdzane zniknięciem nagłówka: kliknięcie w trakcie układu nowo utworzonego panelu
        // bywa pomijane — wtedy kolejne sekcje otwierałyby się poniżej, poza widokiem
        for (let attempt = 0; attempt < 2 && (await app.nodes()).some(n => isHeader(n, name)); attempt++) {
          const button = await toolbarButton(app, name);
          if (!button) break;
          await toggleSection(app, button);
          // Nagłówek znika z warstwy semantycznej z opóźnieniem — ponowne kliknięcie dopiero po 6 s bez zmiany
          for (let t = 0; t < 12 && (await app.nodes()).some(n => isHeader(n, name)); t++) await sleep(500);
        }
      }
    }
  } finally {
    await browser.close();
  }
}

async function accessibility() {
  const browser = await Browser.launch({ width: 1400, height: 900 });
  try {
    const app = new UnoApp(browser);
    await app.open(url);
    // Ekran logowania
    let unnamed = unnamedIn(await app.nodes());
    check(unnamed.length === 0, `logowanie: kontrolki bez nazwy — ${unnamed.length}`);
    await app.login('demo', 'demo');
    await sleep(3000);
    // Każda sekcja osobno: tylko panel sekcji (oraz panele domyślne) w drzewie
    for (const name of sections) {
      const button = await toolbarButton(app, name);
      if (!button) { check(false, `dostępność: brak przycisku „${name}”`); continue; }
      const visible = !!panelHeader(await app.nodes(), name);
      if (!visible) { await toggleSection(app, button); await sleep(2000); }
      unnamed = unnamedIn(await app.nodes());
      check(unnamed.length === 0, `dostępność: sekcja „${name}” — kontrolki bez nazwy: ${unnamed.length}`
        + unnamed.slice(0, 5).map(n => ` [${n.role || n.tag} ${Math.round(n.w)}×${Math.round(n.h)} @${Math.round(n.x)},${Math.round(n.y)} ${n.html}]`).join(''));
      if (!visible) { await toggleSection(app, await toolbarButton(app, name)); await sleep(1000); }
    }
  } finally {
    await browser.close();
  }
}

/**
 * T9.2 — sesja w przeglądarce: po przeładowaniu strony sesja wznawiana z ciasteczka odświeżającego (bez logowania);
 * po wylogowaniu przeładowanie prowadzi do ekranu logowania.
 */
async function session() {
  const browser = await Browser.launch({ width: 1400, height: 900 });
  try {
    await browser.send('Page.enable');
    browser.on(method => { if (method === 'Page.javascriptDialogOpening') browser.send('Page.handleJavaScriptDialog', { accept: true }); });
    const app = new UnoApp(browser);
    await app.open(url);

    // T9.5: odnośnik zgodny z odpowiedzią /downloads/android
    const available = await app.b.eval(`fetch('downloads/android').then(r => r.ok)`);
    const isLink = n => n.label === 'Pobierz aplikację na Android' || n.text.trim() === 'Pobierz aplikację na Android';
    const loginLink = await app.waitFor(isLink, available ? 15000 : 3000);
    check(!!loginLink === available, `pobieranie: odnośnik na ekranie logowania ${available ? 'widoczny' : 'ukryty'} (pakiet ${available ? 'udostępniony' : 'nieudostępniony'})`);

    await app.login('demo', 'demo');
    await sleep(3000);
    await app.b.eval(`document.querySelector('#uno-semantics-root [aria-label="Menu"]').click(); true`);
    const menuLink = await app.waitFor(isLink, available ? 15000 : 3000);
    check(!!menuLink === available, `pobieranie: odnośnik w menu bocznym ${available ? 'widoczny' : 'ukryty'}`);
    await app.b.eval(`document.querySelector('#uno-semantics-root [aria-label="Menu"]').click(); true`);

    await app.open(url);
    const resumed = await app.waitFor(n => n.label === 'Menu', 60000);
    check(!!resumed, 'sesja: po przeładowaniu strony aplikacja bez ponownego logowania');

    if (resumed) {
      await app.b.eval(`document.querySelector('#uno-semantics-root [aria-label="Menu"]').click(); true`);
      const logout = await app.waitFor(n => n.tag === 'BUTTON' && (n.label === 'Wyloguj' || n.text.trim() === 'Wyloguj'), 10000);
      if (logout) await app.b.eval(`[...document.querySelectorAll('#uno-semantics-root button')].find(b => (b.getAttribute('aria-label') || b.textContent).trim() === 'Wyloguj').click(); true`);
      check(!!await app.waitFor(n => n.tag === 'INPUT' && n.label === 'Użytkownik', 30000), 'sesja: wylogowanie — ekran logowania');
      await app.open(url);
      check(!!await app.waitFor(n => n.tag === 'INPUT' && n.label === 'Użytkownik', 60000), 'sesja: po wylogowaniu i przeładowaniu — ekran logowania');
    }
  } finally {
    await browser.close();
  }
}

await session();
await sectionsAt(1400, 900);
await sectionsAt(400, 800);
await accessibility();

console.log(failures.length === 0 ? '\nWszystkie testy zakończone powodzeniem.' : `\nNiepowodzenia: ${failures.length}`);
process.exit(failures.length === 0 ? 0 : 1);
