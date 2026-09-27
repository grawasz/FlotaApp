# Wersja przeglądarkowa — prototyp (T9.1)

## Rozwiązanie

| Element | Stan |
|---|---|
| Platforma docelowa | `net10.0-browserwasm` w projekcie `Flota.App` (Uno WebAssembly, renderer Skia); ten sam kod XAML/C# co Windows, Android i iOS |
| Punkt wejścia | `Platforms/WebAssembly/Program.cs`; adres strony przekazywany do aplikacji jako domyślny adres Flota.Api |
| Adres Flota.Api | pusty `ApiBaseUrl` w konfiguracji — adres strony (aplikacja udostępniana przez Flota.Api); w środowisku deweloperskim `http://localhost:5294/` |
| Tokeny | wyłącznie w pamięci (`InMemoryTokenStore`); przeładowanie strony wymaga ponownego logowania — trwała sesja w T9.2 (ciasteczko `HttpOnly`) |
| Udostępnianie | Flota.Api (`WebApp:RootPath`): pliki wstępnie skompresowane Brotli/gzip, pliki z sumą kontrolną w ścieżce (`/package_*`, `/_framework/*`) z nagłówkiem `immutable`, `index.html` bez pamięci podręcznej, `index.html` dla ścieżek spoza API; testy `WebAppHostingTests` |
| CORS | wyłącznie dla serwera deweloperskiego aplikacji (`Cors:Origins` w `appsettings.Development.json` Flota.Api) |
| Wymagane obciążenie | `wasm-tools` (`dotnet workload install wasm-tools`) — natywne zasoby SkiaSharp dla WebAssembly |

## Uruchomienie

```powershell
dotnet publish src/Flota.App -f net10.0-browserwasm -c Release -o <katalog>
dotnet run --project src/Flota.Api -- --WebApp:RootPath=<katalog>\wwwroot
```

Aplikacja: `http://localhost:5294/`. Wariant deweloperski z przeładowaniem kodu: `dotnet run --project src/Flota.App -f net10.0-browserwasm` (adres `http://localhost:5000/`, API pod `http://localhost:5294/`).

## Pomiar

Build Release (tryb interpretera), Flota.Api na tym samym komputerze, Microsoft Edge bez interfejsu z czystym profilem. Czas od rozpoczęcia nawigacji do wyświetlenia ekranu logowania (ukrycie ekranu ładowania).

| Wielkość | Wartość |
|---|---|
| Rozmiar plików środowiska (`_framework`) | 45,6 MB bez kompresji; 12,2 MB Brotli |
| Przesłane dane przy pierwszym uruchomieniu | 12 258 KB, 200 zasobów |
| Pierwsze uruchomienie (pusta pamięć podręczna) | 4,1–4,4 s |
| Kolejne uruchomienia (pamięć podręczna przeglądarki) | 3,5–5,5 s (pojedyncze przebiegi do 8,5 s); 0 KB przesłanych danych |

Pomiar na komputerze lokalnym nie uwzględnia czasu transmisji przez sieć: przy łączu 50 Mb/s pierwsze uruchomienie wydłuża się o ok. 2 s. Aplikacja Blazor ([POMIAR-BAZOWY.md](POMIAR-BAZOWY.md)) wyświetla stronę od razu, ale dane floty po ok. 18,6 s (geokodowanie sekwencyjne); w nowej architekturze dane floty pochodzą z `/fleet/snapshot` Flota.Api niezależnie od platformy.

### Czas od zalogowania do danych floty

Pomiar na danych symulatora UniAVL (build Release, Edge bez interfejsu, dziennik Flota.Api): od odpowiedzi `POST /auth/login` do żądania `GET /fleet/snapshot` mija 21–23 s, a odpowiedź `/fleet/snapshot` trwa 60–460 ms. Czas zajmuje przeglądarka — budowa strony głównej ze wszystkimi panelami w trybie interpretera WebAssembly. Możliwe działania: tworzenie paneli przy pierwszym otwarciu zamiast przy starcie strony, kompilacja AOT (większy rozmiar pobieranych plików) — do decyzji przy T9.3.

## Weryfikacja do wykonania przez Jurka (⏸)

| Scenariusz | Wynik |
|---|---|
| Logowanie w Chrome i Edge (`http://localhost:5294/`) | |
| Mapa z obiektami, zaznaczenie obiektu, szczegóły | |
| Raport trasy: trasa na mapie, kursy, punkty trasy | |
| Czas od zalogowania do wyświetlenia obiektów na mapie | |
| Czas wygenerowania raportu trasy | |

## Ograniczenia prototypu

- Brak trwałej sesji po przeładowaniu strony (T9.2).
- Pamięć podręczna kafelków mapy i ostatniego stanu floty w wirtualnym systemie plików przeglądarki — trwałość do sprawdzenia w T9.3.
- Eksport XLSX przez okno zapisu pliku — działanie w Firefox i Safari (brak interfejsu File System Access) do sprawdzenia w T9.3.
- Obraz Docker nie zawiera jeszcze aplikacji przeglądarkowej (T9.4).
