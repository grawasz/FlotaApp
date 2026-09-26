# Postęp realizacji

| Zadanie | Stan | Weryfikacja | Uwagi |
|---|---|---|---|
| T0.1 | zakończone | `uno-check --non-interactive --target android --target windows`: brak błędów krytycznych dla Android i Windows | [SRODOWISKO.md](SRODOWISKO.md) |
| T0.2 | zakończone | tabela czasów kompletna dla trzech scenariuszy (start, raport trasy, raport floty); 0 błędów w trakcie pomiaru | [POMIAR-BAZOWY.md](POMIAR-BAZOWY.md); narzędzie w `C:\Projects\FlotaPomiar` do pomiaru porównawczego na punkcie kontrolnym 2 |
| T0.3 | zakończone | — (zadanie bez weryfikacji) | [POMIAR-BAZOWY.md, sekcja „Analiza Addresses.db”](POMIAR-BAZOWY.md#analiza-addressesdb); rekomendacja: bez zasilenia pamięci podręcznej, do decyzji na punkcie kontrolnym 0 |
| T0.4 | zakończone | prototyp uruchomiony na Windows (WinAppSDK, dodatkowo Skia Desktop) i emulatorze Android 15; pomiar odświeżeń mapy w 7 fazach | [PROTOTYPY.md](PROTOTYPY.md#t04--mapsui-spikesmapsuispike); do decyzji na punkcie kontrolnym 0: wariant renderowania Windows (D2), pomiar na fizycznym telefonie |
| T0.5 | zakończone | prototyp uruchomiony na Windows (WinAppSDK; dodatkowo Skia Desktop): 10 000 wierszy, dynamiczne kolumny czujników, zrzuty ekranu | [PROTOTYPY.md](PROTOTYPY.md#t05--datagrid-spikesdatagridspike); wariant Skia — port `DataGrid` niesprawny z Uno 6.7 |
| T0.6 | zakończone | — (zadanie bez weryfikacji) | [PYTANIA.md](PYTANIA.md) — do wysłania przez Jurka |
| T1.1 | zakończone | `git log` zawiera zatwierdzenie początkowe | repozytorium w `C:\Projects\FlotaApp` |
| T1.2 | zakończone | `dotnet build src/Flota.App -f net10.0-windows10.0.26100` i `-f net10.0-android`: 0 błędów; uruchomienie na Windows (pakiet deweloperski MSIX) i emulatorze Android 15 | odstępstwo O6 |
| T1.3 | zakończone | `dotnet build FlotaUno.slnx`: 0 błędów | — |
| T1.4 | w toku | paleta w `Styles/ColorPaletteOverride.xaml`; brak testu porównującego wartości z paletą źródłową | — |
| T1.5 | zakończone | zasoby w `src/Flota.App/Assets/Images`, wyświetlane w aplikacji; ikona aplikacji i ekran startowy odtworzone w SVG z `avl.ico` (Windows, Android — ikona adaptacyjna, iOS) | — |
| T1.6 | w toku | `.github/workflows/ci.yml`: testy, build Windows i Android (`windows-latest`), build iOS dla symulatora (`macos-latest`), obraz Flota.Api z testem `/health` (`ubuntu-latest`); brak przebiegu potoku — repozytorium bez zdalnego GitHub | — |
| T1.7 | w toku | powłoka z panelami sekcji działa na Windows; brak testu interfejsu na szerokości 400 px | — |
| T2.1 | zakończone | deserializacja próbek z `$id`/`$ref`/`$values` — `UniAvlMapperTests` | próbki fikcyjne w `tests/Flota.Api.Tests/Samples` |
| T2.2 | zakończone | `UniAvlClientTests` (WireMock): 401, 5xx, niepoprawny JSON, 204, parametry niezależne od kultury | — |
| T2.3 | zakończone | geokodowanie adresów pozycji w działającej usłudze | dane uwierzytelniające w User Secrets Flota.Api |
| T2.4 | zakończone | `GeocodingServiceTests`: deduplikacja kluczy, pamięć operacyjna, baza, niedostępność Targeo | odstępstwo O4 |
| T2.5 | w toku | baza tworzona przez `EnsureCreated` | odstępstwo O5 |
| T2.6 | zakończone | `AuthIntegrationTests` (WebApplicationFactory, WireMock): logowanie, błędne hasło, rotacja tokenu odświeżania, wylogowanie, 401 UniAVL → 401 | — |
| T2.7 | w toku | migawka floty działa z UniAVL; dopasowanie wiadomości — `UniAvlMapperTests`; brak pomiaru czasu względem [POMIAR-BAZOWY.md](POMIAR-BAZOWY.md) | — |
| T2.8 | w toku | hub `/hubs/fleet` z odpytywaniem UniAVL co 10 s | odstępstwo O1; wymaga K3 |
| T2.9 | w toku | punkty końcowe raportów, kart drogowych, słowników i ustawień; testy mapowania raportu trasy i floty | brak testów porównawczych z aplikacją Blazor |
| T2.10 | zakończone | `docker compose up` (Flota.Api + PostgreSQL 17): `GET /health` → 200 `Healthy`, tabele utworzone w PostgreSQL, `GET /me/settings` bez tokenu → 401; obraz uruchomiony również z SQLite | konfiguracja w `.env` (wzór `.env.example`); obraz uruchamiany bez uprawnień administratora |
| T2.11 | zakończone | `dotnet build src/Flota.Client` | odstępstwo O2 |
| T3.1–T3.7 | w toku | funkcje zaimplementowane i sprawdzone ręcznie na Windows (logowanie, mapa, obiekty, szczegóły, śledzenie); brak testów modelu widoku i testów interfejsu | odstępstwo O3 (T3.2) |
| T4.1–T4.6 | w toku | funkcje zaimplementowane; eksport XLSX — `XlsxExporterTests`; brak testów porównawczych z aplikacją Blazor | odstępstwo O4 (T4.6) |
| T5.1–T5.4 | w toku | funkcje zaimplementowane; brak testów porównawczych i testów modelu widoku | — |
| T5.5 | nierozpoczęte | — | — |
| T9.1 | w toku | build `net10.0-browserwasm` Release; ekran logowania w Edge; pomiar ładowania (4,1–4,4 s pierwsze, 3,5–5,5 s kolejne); `WebAppHostingTests` | [WEB.md](WEB.md); ⏸ oczekuje na weryfikację Jurka: logowanie, mapa, raport trasy w Chrome i Edge |

## Odstępstwa od planu

| Nr | Zadanie | Odstępstwo | Uzasadnienie |
|---|---|---|---|
| O1 | T2.8 | Aktualizacje pozycji w hubie pochodzą z odpytywania `TrackingMessages/Last` co 10 s zamiast z połączenia WebSocket UniAVL. | Brak dokumentacji WebSocket UniAVL (K3); kontrakt huba po stronie aplikacji pozostaje bez zmian. |
| O2 | T2.11 | `Flota.Client` napisany ręcznie (`FlotaApiClient`) zamiast generowania Kiota. | Kontrakty współdzielone z `Flota.Core` z generatorem `FlotaJsonContext`; brak powielenia modeli. |
| O3 | T3.2 | Ostatni stan floty zapisywany w pliku JSON (`SnapshotCache`) zamiast lokalnej bazy SQLite. | Jeden dokument odczytywany i zapisywany w całości; bez zależności od SQLite w aplikacji. |
| O4 | T2.4, T4.6 | Pamięć podręczna adresów: `IMemoryCache` zamiast `HybridCache`. Plik XLSX generowany w Flota.Api (`POST /export/xlsx`) zamiast w aplikacji. | `IMemoryCache` wystarcza dla jednej instancji usługi. ClosedXML poza aplikacją mobilną zmniejsza rozmiar pakietu. |
| O5 | T2.5 | Schemat bazy tworzony przez `EnsureCreated` zamiast migracji. | Dostawca bazy produkcyjnej nierozstrzygnięty (K6). |
| O6 | T1.2 | Wersja Windows uruchamiana jako zarejestrowany pakiet deweloperski MSIX. | Uno.Material i lokalizacja zasobów wymagają tożsamości pakietu WinAppSDK. |
| O7 | T0.5, T4.3 | Tabele raportów w kontrolce własnej `FlotaTable` zamiast `DataGrid`. | Port `DataGrid` niesprawny z Uno 6.7 (wynik T0.5). |

## Testy automatyczne

| Projekt | Liczba testów | Wynik |
|---|---|---|
| `Flota.Core.Tests` | 70 | wszystkie zakończone powodzeniem |
| `Flota.Api.Tests` | 36 | wszystkie zakończone powodzeniem |
