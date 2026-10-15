# Testy interfejsu

## Testy statyczne zasobów (`tests/Flota.App.Tests`)

Testy xUnit uruchamiane bez budowania aplikacji Uno; analizują pliki XAML, `Resources.resw` i plan migracji.

| Test | Sprawdzenie | Zadanie |
|---|---|---|
| `PaletteTests.LightTheme_MatchesPlanTable` | wartości kolorów motywu jasnego zgodne z tabelą 7.1 planu (zasoby i pędzle) | T1.4 |
| `PaletteTests.DarkTheme_*` | motyw ciemny definiuje te same zasoby; kolory statusów bez zmian | T1.4, T7.2 |
| `ViewRulesTests.InteractiveControls_HaveAccessibleName` | każda kontrolka interaktywna widoków ma nazwę dostępną (`AutomationProperties.Name`, treść, nagłówek, etykieta paska narzędzi) | T7.4 |
| `ViewRulesTests.Uids_HaveResourceEntries` | każde `x:Uid` ma wpis w `Resources.resw` | — |
| `ViewRulesTests.CodeTextKeys_ExistInResources` | każdy klucz `Loc.Get` / `Loc.Format` w kodzie ma wpis w `Resources.resw` | — |
| `ViewRulesTests.Views_UsePaletteResourcesOnly` | brak wartości kolorów w widokach | — |

## Testy wersji przeglądarkowej (`tools/TestyUi`)

Skrypt Node.js (wersja 22 lub nowsza, bez zależności zewnętrznych) steruje przeglądarką Edge lub Chrome bez interfejsu przez Chrome DevTools Protocol. Elementy aplikacji są odnajdywane w warstwie semantycznej Uno (`#uno-semantics-root`), tworzonej dla czytników ekranu — po nazwach dostępnych, bez współrzędnych ekranu.

| Scenariusz | Sprawdzenie | Zadanie |
|---|---|---|
| Sekcje, 1400 px | każda z 12 sekcji paska wyświetla swój panel | T1.7 |
| Sekcje, 400 px | jak wyżej; pasek sekcji przewijany w poziomie | T1.7 |
| Dostępność | ekran logowania i każda sekcja: kontrolki interaktywne bez nazwy dostępnej (`aria-label`) | T7.4 |
| Sesja | po zalogowaniu i przeładowaniu strony aplikacja bez ponownego logowania; wylogowanie — ekran logowania, również po przeładowaniu | T9.2 |
| Pobieranie | odnośnik „Pobierz aplikację na Android” na ekranie logowania i w menu bocznym widoczny wyłącznie, gdy `/downloads/android` zwraca 200 | T9.5 |

Elementy pasków przewijania (strzałki i suwak, do 16 px), które Uno wystawia jako przyciski bez nazwy, są pomijane — nie pochodzą z widoków aplikacji.

### Uruchomienie

```powershell
dotnet publish src/Flota.App -f net10.0-browserwasm -c Release -o <katalog>
./tools/Start-Symulacja.ps1 -WebApp <katalog>\wwwroot
node tools/TestyUi/testy.mjs http://localhost:5294/ <katalog zrzutów>
```

Logowanie kontem symulatora ([SYMULATOR.md](SYMULATOR.md)). Kod wyjścia 0 — wszystkie sprawdzenia zakończone powodzeniem. Opcjonalny katalog zrzutów zawiera obraz każdej sekcji w obu szerokościach. Przeglądarka: zmienna `BROWSER` lub domyślna instalacja Edge/Chrome.

Przeglądarka bez interfejsu potrafi nie potwierdzić zdarzenia kółka myszy, choć strona działa (JavaScript odpowiada w 1–2 ms); sterownik czeka na potwierdzenie kółka najwyżej 3 s, a na pozostałe wywołania — 60 s (błąd zamiast oczekiwania bez końca). Zmienna `FLOTA_UI_DEBUG=1` włącza dziennik zdarzeń wejścia i wywołań trwających ponad 2 s.

Przełączanie sekcji: przycisk przewijany do widocznej części paska (położenie odczytywane po ustaniu ruchu paska — zdarzenia kółka wysłane w trakcie budowy panelu bywają przetwarzane z opóźnieniem), następnie wywołanie przez element semantyczny przycisku (współrzędne węzłów paska po przewinięciu bywają nieaktualne). Zamknięcie sekcji potwierdzane zniknięciem nagłówka. Przy niepowodzeniu skrypt zapisuje zrzut ekranu w katalogu tymczasowym oraz wypisuje widoczne nagłówki i błędy konsoli strony.

Po ponownej publikacji aplikacji przeglądarkowej Flota.Api należy uruchomić ponownie — działający proces może udostępniać pliki poprzedniej wersji.

### Wynik

| Data | Wersja | Wynik |
|---|---|---|
| 2026-09-27 | zatwierdzenie z T1.4, T1.7, T7.4 | 37 sprawdzeń zakończonych powodzeniem (24 — sekcje, 13 — dostępność) |
| 2026-09-27 | zatwierdzenie z T7.2 (motywy, ikony jednobarwne) | 37 sprawdzeń zakończonych powodzeniem |
| 2026-10-12 | zatwierdzenie z T9.3a (panele przy pierwszym wyświetleniu) | 37 sprawdzeń zakończonych powodzeniem w trzech kolejnych przebiegach |
| 2026-10-15 | zatwierdzenie z T9.2 (sesja w ciasteczku) | 40 sprawdzeń zakończonych powodzeniem (3 — sesja, 24 — sekcje, 13 — dostępność) |
| 2026-10-15 | T9.5, pakiet testowy w katalogu pobierania | 42 sprawdzenia zakończone powodzeniem (2 — pobieranie, 3 — sesja, 24 — sekcje, 13 — dostępność) |

## Ograniczenia

- Test interfejsu obejmuje wersję przeglądarkową; na Windows, Android i iOS nazwy dostępne pochodzą z tych samych plików XAML i zasobów (testy statyczne).
- Obszar dotyku części przycisków (np. przycisk statusu na liście obiektów — 30 px, zamknięcie dymka mapy — 24 px) jest mniejszy niż zalecane 48 dp; układ odwzorowuje aplikację Blazor, zmiana należy do usprawnień T7.3.
- W układzie wąskim (400 px) formularze parametrów raportu floty i Eco-Driving są szersze niż ekran (przewijanie w poziomie w obrębie panelu).
