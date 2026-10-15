# Wersja przeglądarkowa

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
| Start strony głównej | żądanie `/fleet/snapshot` wysyłane przed budową strony głównej (`App.ShowShell`), równolegle z odczytem stanu zapisanego; panele poza mapą tworzone przy pierwszym wyświetleniu (`ShellPage.EnsurePanel`) |
| Kompilacja AOT | profilowana kompilacja AOT w buildzie Release (profil `Platforms/WebAssembly/aot.profile`); wyłączenie: `-p:FlotaWasmAot=false` — [kompilacja AOT](#kompilacja-aot) |

## Uruchomienie

```powershell
dotnet publish src/Flota.App -f net10.0-browserwasm -c Release -o <katalog>
dotnet run --project src/Flota.Api -- --WebApp:RootPath=<katalog>\wwwroot
```

Aplikacja: `http://localhost:5294/`. Wariant deweloperski z przeładowaniem kodu: `dotnet run --project src/Flota.App -f net10.0-browserwasm` (adres `http://localhost:5000/`, API pod `http://localhost:5294/`). Na danych symulatora UniAVL: `./tools/Start-Symulacja.ps1 -WebApp <katalog>\wwwroot` (login `demo`, hasło `demo`).

## Wydajność (T9.3a)

### Metoda

| | |
|---|---|
| Narzędzie | `tools/TestyUi/pomiar.mjs` — Microsoft Edge bez interfejsu sterowany protokołem CDP, każdy przebieg w czystym profilu przeglądarki |
| Dane | symulator UniAVL (`tools/Start-Symulacja.ps1`), Flota.Api i przeglądarka na tym samym komputerze (bez opóźnień sieci) |
| Build | Release; aplikacja udostępniana przez Flota.Api (pliki wstępnie skompresowane Brotli) |
| Kolejność | bloki po 3 przebiegi na przemian: a → b → c → c → b → a; wynik: mediana (zakres) ze wszystkich przebiegów wariantu |
| Znaczniki | `Flota.App/Services/Pomiar.cs` — wpisy w konsoli przeglądarki przy parametrze adresu `?pomiar=1`; bez parametru brak działania |

| Wielkość | Definicja |
|---|---|
| Pierwsze otwarcie | od nawigacji do usunięcia ekranu ładowania (ekran logowania), pusta pamięć podręczna |
| Ponowne otwarcie | jak wyżej, pliki z pamięci podręcznej przeglądarki (0 KB przesłanych danych) |
| Żądanie stanu floty | od odpowiedzi `POST /auth/login` do wysłania `GET /fleet/snapshot` |
| Obiekty na liście | od odpowiedzi `POST /auth/login` do pojawienia się obiektu na liście (warstwa semantyczna) |
| Otwarcie panelu raportu trasy | od kliknięcia „Raport trasy” do gotowości przycisku „Generuj raport (trasa)” |
| Raport trasy | od kliknięcia „Generuj raport (trasa)” do odebrania odpowiedzi API (trasa, punkty trasy) i zwolnienia wątku strony |

### Warianty

| Wariant | Zmiana |
|---|---|
| a | stan wyjściowy: wszystkie panele tworzone przy budowie strony głównej, `/fleet/snapshot` po zakończeniu budowy strony |
| b | panele poza mapą tworzone przy pierwszym wyświetleniu; `/fleet/snapshot` wysyłane przed budową strony głównej, równolegle z odczytem stanu zapisanego |
| c | b + profilowana kompilacja AOT (Uno.Wasm.Bootstrap `InterpreterAndAOT`, profil z sesji: logowanie, lista, mapa, raport trasy, panele raportów i słowników) |

### Wyniki

| Wielkość | a | b | c (AOT) |
|---|---:|---:|---:|
| Pierwsze otwarcie | 5,3 s (5,0–5,4) | 6,1 s (5,1–7,0) | 6,1 s (5,6–6,6) |
| Ponowne otwarcie | 4,2 s (3,6–6,0) | 4,4 s (4,0–5,9) | 2,8 s (2,3–3,0) |
| Żądanie stanu floty po zalogowaniu | 8,2 s (8,0–8,4) | 0,2 s (0,2–0,3) | 0,2 s (0,1–0,2) |
| Obiekty na liście po zalogowaniu | 10,5 s (10,2–10,6) | 4,2 s (3,8–4,9) | 2,4 s (2,0–3,0) |
| Otwarcie panelu raportu trasy | 0,3 s (0,3–0,3) | 2,3 s (2,0–2,8) | 0,8 s (0,6–1,3) |
| Raport trasy | 2,9 s (2,6–3,6) | 3,2 s (2,9–3,6) | 2,2 s (1,4–2,4) |
| Pliki `_framework` bez kompresji | 45,6 MB | 45,6 MB | 64,9 MB |
| Pliki `_framework` Brotli | 12,2 MB | 12,2 MB | 15,3 MB |
| Przesłane dane przy pierwszym otwarciu | 12 295 KB | 12 283 KB | 15 454 KB |
| Czas buildu Release | 3 min | 3 min | 10–11 min |

Liczba przebiegów: a — 4 (2 przerwane błędem narzędzia przy pierwszym otwarciu), b — 6, c — 6. Pierwsze otwarcie wariantów a i b (ten sam rozmiar plików, ten sam kod do ekranu logowania) różni się o 0,8 s — rozrzut pomiaru na komputerze lokalnym jest tego rzędu.

Wariant b przenosi koszt budowy panelu raportu trasy z logowania na pierwsze otwarcie panelu (2,3 s zamiast 0,3 s); pozostałe panele — analogicznie.

### Porównanie z aplikacją Blazor

| | Aplikacja Blazor | Wersja przeglądarkowa b | Wersja przeglądarkowa c (AOT) |
|---|---:|---:|---:|
| Dane floty po zalogowaniu | 18,6 s | 4,2 s | 2,4 s |
| Raport trasy | 80,4 s | 3,2 s | 2,2 s |

Ograniczenia porównania: wartości aplikacji Blazor ([POMIAR-BAZOWY.md](POMIAR-BAZOWY.md)) obejmują wywołania UniAVL i Targeo na koncie produkcyjnym bez renderowania interfejsu; wartości wersji przeglądarkowej — symulator UniAVL bez geokodowania, z renderowaniem interfejsu. Na danych rzeczywistych odpowiedź `/fleet/snapshot` wydłuża się o czas wywołań UniAVL wykonywanych równolegle (w pomiarze bazowym najdłuższe: `TrackingMessages/Last`, 1,25 s) i budżet geokodowania (do 2 s); żądanie jest wysyłane 0,2 s po zalogowaniu, równolegle z budową strony (2,3 s w wariancie b, 1,4 s w wariancie c). Raport trasy aplikacji Blazor obejmuje geokodowanie wszystkich punktów trasy (76 s), w nowej architekturze wykonywane przez Flota.Api z pamięcią podręczną. Pomiar na danych rzeczywistych wymaga logowania Jurka.

### Inne źródła opóźnień

Znaczniki aplikacji, czasy od odpowiedzi `POST /auth/login`: wariant b — mediana 5 przebiegów, wariant c — pojedynczy przebieg.

| Znacznik | b | c (AOT) | Opis |
|---|---:|---:|---|
| `shell.created` | 0,58 s | 0,27 s | strona główna utworzona (XAML) |
| `panel.objects.start` | 1,34 s | 0,67 s | pierwszy przebieg układu strony — utworzenie paneli Obiekty i Szczegóły obiektu |
| `shell.loaded` / `map.init.start` | 1,84 s | 0,95 s | strona załadowana, inicjalizacja mapy (Mapsui) |
| `objects.applied` | 2,38 s | 1,39 s | stan floty zastosowany — odpowiedź gotowa od ok. 0,25 s, oczekuje na wolny wątek strony |
| `objects.rendered` | 4,17 s | 2,15 s | pierwsza klatka z obiektami na liście |
| `map.images.loaded` | 4,75 s | 2,81 s | ikony typów obiektów dla mapy wczytane |
| `map.rendered` | 4,88 s | 2,92 s | pierwsza klatka mapy z obiektami |

| Źródło | Wielkość | Stan |
|---|---|---|
| Jeden wątek strony (WebAssembly) | odpowiedź `/fleet/snapshot` czeka ok. 2,1 s (b) na zakończenie budowy strony | wariant b ogranicza budowę do paneli widocznych; dalsze skrócenie — kompilacja AOT (c) |
| Zastosowanie stanu floty i pierwsza klatka listy | 1,8 s (b), 0,8 s (c) | lista obiektów, szczegóły obiektu i warstwa obiektów mapy w jednym przebiegu |
| Ikony mapy | odczyt sekwencyjny plików PNG ikon typów obiektów (żądanie HTTP na plik); odczyt równoległy skraca czas do pierwszej klatki mapy z obiektami o ok. 0,4 s (4,88 → 4,45 s; osobna seria b → b z odczytem równoległym, 5 przebiegów na wariant, pozostałe wielkości bez zmian w granicach rozrzutu) | wprowadzony odczyt równoległy (`MapImages.LoadAsync`) |
| Pierwsze otwarcie prawego panelu | 2,0 s (b), z czego utworzenie panelu 0,33 s; reszta — przebieg układu po zmianie podziału kolumn (mapa 9/12 → 4/12) i pierwsza klatka panelu | bez zmian; do oceny w T9.3 |
| Dane ICU w `Flota.App.wasm` | 5,5 MB (ok. 2,3 MB Brotli) — zasób `icudt.dat` dodawany przez pakiet `Uno.icu-wasm` (układ tekstu renderera Skia), niezależny od danych ICU środowiska .NET (`icudt_*.dat`, 0,6 MB Brotli) | bez zmian — wymagany przez renderer |
| Czcionki i obrazy | pobierane na żądanie; przy pierwszym otwarciu łącznie poniżej 50 KB | bez zmian |
| Uruchomienie aplikacji | `app.launch` 2,6 s od nawigacji (pobranie i inicjalizacja środowiska), pierwsza klatka ekranu logowania 4,5 s | kompilacja AOT skraca ponowne otwarcie (c: 2,8 s) |

Parametr adresu bez wartości (np. `/?abc`) blokuje uruchomienie aplikacji: Uno przekazuje parametry adresu jako argumenty wiersza poleceń hosta, a argument bez wartości przerywa konfigurację. Parametr z wartością (`/?abc=1`) działa. Do poprawy w T9.3.

### Kompilacja AOT

Build Release wersji przeglądarkowej korzysta z profilowanej kompilacji AOT (wariant c; decyzja Jurka na punkcie kontrolnym T9.3a). Bilans:

| Zysk | Koszt |
|---|---|
| obiekty na liście po zalogowaniu: 2,4 s zamiast 4,2 s | pobranie przy pierwszym otwarciu: +3,2 MB (Brotli), przy łączu 50 Mb/s ok. +0,5 s |
| ponowne otwarcie: 2,8 s zamiast 4,4 s | build Release: 10–11 min zamiast 3 min (potok CI, obraz Docker) |
| otwarcie panelu raportu trasy: 0,8 s zamiast 2,3 s; raport trasy: 2,2 s zamiast 3,2 s | profil AOT (5 MB) w repozytorium, odświeżany po istotnych zmianach kodu; nieaktualny profil obniża zysk, nie powoduje błędów |

Odświeżenie profilu (build zbierający profil działa bez AOT):

```powershell
dotnet publish src/Flota.App -f net10.0-browserwasm -c Release -o <katalog> -p:WasmShellGenerateAOTProfile=true
./tools/Start-Symulacja.ps1 -WebApp <katalog>\wwwroot
node tools/TestyUi/profil-aot.mjs http://localhost:5294/ <katalog profilu>
Copy-Item <katalog profilu>\aot.profile src/Flota.App/Platforms/WebAssembly/aot.profile
```

Katalog pośredni buildu AOT wymaga krótkiej ścieżki: pliki pośrednie kompilatora przekraczają limit 260 znaków ścieżki Windows przy katalogach głęboko w profilu użytkownika (wtedy `--artifacts-path` z krótką ścieżką, np. `artifacts\aot`). Build z katalogu repozytorium mieści się w limicie.

## Weryfikacja do wykonania przez Jurka

| Scenariusz | Wynik |
|---|---|
| Logowanie w Chrome i Edge (`http://localhost:5294/`) | |
| Mapa z obiektami, zaznaczenie obiektu, szczegóły | |
| Raport trasy: trasa na mapie, kursy, punkty trasy | |
| Czas od zalogowania do wyświetlenia obiektów na mapie | |
| Czas wygenerowania raportu trasy | |

## Ograniczenia

- Brak trwałej sesji po przeładowaniu strony (T9.2).
- Pamięć podręczna kafelków mapy i ostatniego stanu floty w wirtualnym systemie plików przeglądarki — trwałość do sprawdzenia w T9.3.
- Eksport XLSX przez okno zapisu pliku — działanie w Firefox i Safari (brak interfejsu File System Access) do sprawdzenia w T9.3.
- Obraz Docker nie zawiera jeszcze aplikacji przeglądarkowej (T9.4).
