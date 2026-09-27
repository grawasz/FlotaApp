// Testy interfejsu wersji przeglądarkowej na danych symulatora UniAVL (docs/TESTY-UI.md).
//   T1.7 — przejście do każdej sekcji przy szerokości 400 px i 1400 px
//   T7.4 — nazwa dostępna (aria-label) każdej kontrolki interaktywnej we wszystkich sekcjach
// Użycie: node tools/TestyUi/testy.mjs [adres] [katalog zrzutów]
import { mkdirSync } from 'node:fs';
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
 * Przycisk sekcji na pasku, w całości w widocznej części paska (prawą część zajmują wskaźnik połączenia i menu);
 * w razie potrzeby pasek przewijany w poziomie.
 */
async function toolbarButton(app, name) {
  let previous;
  for (let i = 0; i < 16; i++) {
    const nodes = await app.nodes();
    const menu = nodes.find(x => x.tag === 'BUTTON' && x.label === 'Menu' && x.y < toolbarHeight);
    const limit = (menu?.x ?? app.b.width) - (app.b.width >= 600 ? 130 : 60);
    const n = nodes.find(x => isToolbarButton(x, name));
    if (n && n.x >= 0 && n.x + n.w <= limit) return n;
    // Koniec paska: przewijanie nie zmienia już położenia przycisku
    if (n && previous && n.x === previous.x) return n;
    previous = n;
    await app.b.wheel(Math.min(limit, app.b.width) / 2, toolbarHeight / 2, n && n.x < 0 ? -120 : 120);
    await sleep(300);
  }
  return null;
}

async function sectionsAt(width, height) {
  const browser = await Browser.launch({ width, height });
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
        await app.clickNode(button);
        await sleep(1500);
      }
      const header = await app.waitFor(n => isHeader(n, name), 5000);
      check(!!header, `${width} px: sekcja „${name}” wyświetlona`);
      if (shots) await browser.screenshot(join(shots, `sekcja-${width}-${sections.indexOf(name) + 1}.png`));
      if (!shown) {
        const button = await toolbarButton(app, name);
        if (button) { await app.clickNode(button); await sleep(1000); }
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
      if (!visible) { await app.clickNode(button); await sleep(2000); }
      unnamed = unnamedIn(await app.nodes());
      check(unnamed.length === 0, `dostępność: sekcja „${name}” — kontrolki bez nazwy: ${unnamed.length}`
        + unnamed.slice(0, 5).map(n => ` [${n.role || n.tag} ${Math.round(n.w)}×${Math.round(n.h)} @${Math.round(n.x)},${Math.round(n.y)} ${n.html}]`).join(''));
      if (!visible) { await app.clickNode(await toolbarButton(app, name)); await sleep(1000); }
    }
  } finally {
    await browser.close();
  }
}

await sectionsAt(1400, 900);
await sectionsAt(400, 800);
await accessibility();

console.log(failures.length === 0 ? '\nWszystkie testy zakończone powodzeniem.' : `\nNiepowodzenia: ${failures.length}`);
process.exit(failures.length === 0 ? 0 : 1);
