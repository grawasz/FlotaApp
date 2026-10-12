// Zebranie profilu AOT (Uno.Wasm.Bootstrap, WasmShellGenerateAOTProfile=true) na danych symulatora UniAVL:
// logowanie, lista obiektów, mapa, raport trasy, panele raportów i słowników; zapis aot.profile do katalogu.
// Użycie: node tools/TestyUi/profil-aot.mjs <adres> <katalog docelowy>
import { existsSync, mkdirSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { Browser, UnoApp, sleep } from './cdp.mjs';

const url = process.argv[2] ?? 'http://localhost:5294/';
const target = resolve(process.argv[3] ?? '.');
mkdirSync(target, { recursive: true });

const sections = ['Raport trasy', 'Raport floty', 'Ranking Eco-Driving', 'Karty drogowe', 'Wyposażenie', 'Zdarzenia', 'Kierowcy', 'Obszary'];
const browser = await Browser.launch({ width: 1400, height: 900, port: 9480 });
try {
  await browser.send('Browser.setDownloadBehavior', { behavior: 'allow', downloadPath: target });
  await browser.send('Page.enable');
  browser.on(method => { if (method === 'Page.javascriptDialogOpening') browser.send('Page.handleJavaScriptDialog', { accept: true }); });
  const app = new UnoApp(browser);
  await app.open(url, 180000);
  await app.login('demo', 'demo');
  await sleep(5000);

  // Zaznaczenie obiektu i raport trasy z trasą na mapie
  const object = await app.waitFor(n => n.text.trim() === 'Renault Trafic', 60000);
  if (object) { await app.clickNode(object); await sleep(2000); }
  for (const name of sections) {
    const button = (await app.nodes()).find(n => n.tag === 'BUTTON' && n.label === name && n.y < 44);
    if (!button) { console.log(`brak przycisku: ${name}`); continue; }
    await app.clickNode(button);
    await sleep(4000);
    if (name === 'Raport trasy') {
      await browser.eval(`document.querySelector('#uno-semantics-root [aria-label="Generuj raport (trasa)"]')?.click(); true`);
      await sleep(10000);
    }
    console.log(`sekcja: ${name}`);
  }

  // Zapis profilu: pobranie pliku aot.profile
  // Zapis wywoływany skrótem Shift+Alt+P (metoda instancji AotProfilerSupport)
  await browser.eval("document.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyP', shiftKey: true, altKey: true })); true");
  for (let i = 0; i < 60 && !existsSync(resolve(target, 'aot.profile')); i++) await sleep(1000);
  console.log(existsSync(resolve(target, 'aot.profile')) ? `zapisano: ${resolve(target, 'aot.profile')}` : `brak pliku profilu (${readdirSync(target).join(', ')})`);
} finally {
  await browser.close();
}
