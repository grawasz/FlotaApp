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
| T1.4 | zakończone | `PaletteTests`: wartości motywu jasnego zgodne z tabelą 7.1 planu (13 zasobów, w tym pędzle statusów); motyw ciemny kompletny | [TESTY-UI.md](TESTY-UI.md) |
| T1.5 | zakończone | zasoby w `src/Flota.App/Assets/Images`, wyświetlane w aplikacji; ikona aplikacji i ekran startowy odtworzone w SVG z `avl.ico` (Windows, Android — ikona adaptacyjna, iOS) | — |
| T1.6 | w toku | `.github/workflows/ci.yml`: testy, build Windows i Android (`windows-latest`), build iOS dla symulatora (`macos-latest`), obraz Flota.Api z testem `/health` (`ubuntu-latest`); brak przebiegu potoku — repozytorium bez zdalnego GitHub | — |
| T1.7 | zakończone | `tools/TestyUi` (przeglądarka, symulator UniAVL): każda z 12 sekcji wyświetlana przy 400 px i 1400 px; pasek sekcji przewijany w poziomie na wąskim ekranie | [TESTY-UI.md](TESTY-UI.md) |
| T2.1 | zakończone | deserializacja próbek z `$id`/`$ref`/`$values` — `UniAvlMapperTests` | próbki fikcyjne w `tests/Flota.Api.Tests/Samples` |
| T2.2 | zakończone | `UniAvlClientTests` (WireMock): 401, 5xx, niepoprawny JSON, 204, parametry niezależne od kultury | — |
| T2.3 | zakończone | geokodowanie adresów pozycji w działającej usłudze | dane uwierzytelniające w User Secrets Flota.Api |
| T2.4 | zakończone | `GeocodingServiceTests`: deduplikacja kluczy, pamięć operacyjna, baza, niedostępność Targeo | odstępstwo O4 |
| T2.5 | w toku | baza tworzona przez `EnsureCreated` | odstępstwo O5 |
| T2.6 | zakończone | `AuthIntegrationTests` (WebApplicationFactory, WireMock): logowanie, błędne hasło, rotacja tokenu odświeżania, wylogowanie, 401 UniAVL → 401 | — |
| T2.7 | w toku | migawka floty działa z UniAVL; dopasowanie wiadomości — `UniAvlMapperTests`; brak pomiaru czasu względem [POMIAR-BAZOWY.md](POMIAR-BAZOWY.md) | — |
| T2.8 | w toku | hub `/hubs/fleet` z odpytywaniem UniAVL co 10 s; `FleetHubTests` (symulator UniAVL): pierwsze odpytanie — wszystkie obiekty, kolejne — wyłącznie zmienione pozycje | odstępstwo O1; test z atrapą WebSocket UniAVL wymaga K3 |
| T2.9 | zakończone | `ApiEndpointsTests`: każdy punkt końcowy wywoływany klientem `FlotaApiClient` na danych symulatora UniAVL (stan floty, trasa, punkty trasy, ślad, paliwo z redukcją serii LTTB, zdarzenia, styl jazdy, podsumowanie kursów, raport floty, Eco-Driving, karty drogowe, słowniki, ustawienia, eksport XLSX) | [SYMULATOR.md](SYMULATOR.md) |
| T2.10 | zakończone | `docker compose up` (Flota.Api + PostgreSQL 17): `GET /health` → 200 `Healthy`, tabele utworzone w PostgreSQL, `GET /me/settings` bez tokenu → 401; obraz uruchomiony również z SQLite | konfiguracja w `.env` (wzór `.env.example`); obraz uruchamiany bez uprawnień administratora |
| T2.11 | zakończone | `dotnet build src/Flota.Client` | odstępstwo O2 |
| T3.1 | w toku | logowanie, odświeżanie tokenu, wylogowanie sprawdzone ręcznie (Windows, przeglądarka); wygaśnięcie sesji — `PanelViewModelTests.SessionExpired_NoErrorNotice` | brak testu interfejsu logowania; logika logowania w kodzie strony |
| T3.2 | w toku | start ze stanu zapisanego, następnie odświeżenie — `FleetViewModelTests.LoadAsync_CachedSnapshotFirst_ThenRefreshAndLiveFeed` | odstępstwo O3; brak pomiaru czasu startu na urządzeniu |
| T3.3 | w toku | mapa z obiektami, zaznaczenie, wyśrodkowanie, obszary — sprawdzone ręcznie i na zrzutach (przeglądarka, symulator UniAVL); wyśrodkowanie po wyborze — `FleetViewModelTests.Select_CentersMapAndShowsObject` | brak testu interfejsu |
| T3.4 | zakończone | `FleetViewModelTests`: sortowanie (status, nazwa, lokalizacja, odwrócenie kierunku), filtr (nazwa, numer rejestracyjny, lokalizacja), widoczność na mapie | — |
| T3.5 | w toku | szczegóły obiektu wyświetlane; klasyfikacja urządzenia — testy `Flota.Core` | brak testu porównawczego z aplikacją Blazor |
| T3.6 | zakończone | `FleetViewModelTests` z atrapą strumienia pozycji (`IFleetLiveFeed`): aktualizacja pozycji, adresy, przekazanie do wątku interfejsu, tryb „Śledź”; `FleetHubTests`: hub Flota.Api na symulatorze UniAVL | wznawianie połączenia sprawdzone ręcznie |
| T3.7 | zakończone | `FleetViewModelTests.DefaultView_LoadGoSaveDelete`; `ApiEndpointsTests.Settings_SaveAndRead` | — |
| T4.1 | zakończone | `RouteReportViewModelTests`: okres domyślny, kryteria postoju (co najmniej jedno aktywne), parametry zapytania, wybór obiektu | — |
| T4.2 | w toku | kursy, trasa na mapie, strzałki kierunku, odtwarzanie — `RouteReportViewModelTests` (punkty trasy, nawigacja, odtwarzanie co 3 s, limit 5000 punktów) | brak testu porównawczego etapów z aplikacją Blazor |
| T4.3 | w toku | kolumny czujników punktów trasy — `ApiEndpointsTests.Waypoints_PointsAndSensorColumns` | brak testu porównawczego z aplikacją Blazor |
| T4.4 | w toku | tankowania, ubytki, wykresy — `ApiEndpointsTests.Fuel_RefuelingAndDownsampledSeries` | brak testu porównawczego sum z aplikacją Blazor |
| T4.5 | w toku | zdarzenia, styl jazdy, podsumowanie kursów — `ApiEndpointsTests` | brak testów porównawczych z aplikacją Blazor |
| T4.6 | zakończone | `XlsxExporterTests`: nazwy arkuszy, nagłówki, formaty liczb | odstępstwo O4 |
| T5.1 | w toku | raport floty — `FleetReportViewModel` (`PanelViewModelTests`), `ApiEndpointsTests.FleetReport_SummaryAndDailyDetails` | brak testu porównawczego z plikiem z aplikacji Blazor |
| T5.2 | w toku | ranking Eco-Driving — `PanelViewModelTests.EcoDriving_FullDaysAndRanking`, `ApiEndpointsTests.EcoDriving_RankingByScore` | brak testu porównawczego z aplikacją Blazor |
| T5.3 | zakończone | `PanelViewModelTests.Logbooks_LoadListThenEntries` (`LogbooksViewModel`) | — |
| T5.4 | zakończone | `PanelViewModelTests`: kierowcy (wczytanie jednorazowe, ponowienie po błędzie), wyposażenie (odświeżenie pamięci podręcznej Flota.Api), zdarzenia i terminy; obszary — `FleetViewModelTests.ActiveAreas_SortedByNameIgnoringCase` | — |
| T5.5 | nierozpoczęte | — | — |
| T7.2 | zakończone | zrzuty obu motywów (przeglądarka, 1400 px i 400 px) w [MOTYWY.md](MOTYWY.md); wybór motywu w menu bocznym, domyślnie jasny; `PaletteTests`, `ThemeChoiceTests` | logo w motywie ciemnym — wariant do dostarczenia |
| T7.4 | zakończone | `ViewRulesTests` (nazwa dostępna każdej kontrolki interaktywnej w XAML, teksty w `.resw`); `tools/TestyUi`: ekran logowania i 12 sekcji bez kontrolek bez nazwy dostępnej | obszary dotyku mniejsze niż 48 dp — [TESTY-UI.md](TESTY-UI.md#ograniczenia) |
| T9.1 | zakończone | build `net10.0-browserwasm` Release; ekran logowania w Edge; pomiar ładowania (4,1–4,4 s pierwsze, 3,5–5,5 s kolejne); `WebAppHostingTests`; punkt kontrolny: decyzja Jurka o kontynuacji fazy 9 (2026-10-12) | [WEB.md](WEB.md); tabela weryfikacji w Chrome i Edge do uzupełnienia przez Jurka |
| T9.3a | w toku | `tools/TestyUi/pomiar.mjs` (Release, Edge bez interfejsu, symulator UniAVL, bloki na przemian): obiekty na liście po zalogowaniu — stan wyjściowy 10,5 s, panele przy pierwszym wyświetleniu i wcześniejsze `/fleet/snapshot` 4,2 s, z kompilacją AOT 2,4 s; ikony mapy wczytywane równolegle (−0,4 s do mapy z obiektami); `FleetViewModelTests`, `RouteReportViewModelTests.CreatedAfterSelection_TakesSelectedObject`; `tools/TestyUi` bez błędów | [WEB.md](WEB.md#wydajność-t93a); ⏸ oczekuje na decyzję Jurka o włączeniu AOT do buildu Release (domyślnie wyłączone, `-p:FlotaWasmAot=true`) |

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
| O8 | T1.3, T3.x–T5.x | Modele widoku w osobnym projekcie `Flota.Presentation` (net10.0, bez zależności od Uno); zależności platformowe przez interfejsy `ISnapshotStore`, `IFleetLiveFeed` i `TimeProvider`. Projekt spoza struktury z [5.3](Plan-migracji-Uno.md#53-struktura-rozwiązania). | Testy modeli widoku bez uruchamiania aplikacji Uno (`Flota.Presentation.Tests`). |

## Testy automatyczne

| Projekt | Liczba testów | Wynik |
|---|---|---|
| `Flota.Core.Tests` | 70 | wszystkie zakończone powodzeniem |
| `Flota.Api.Tests` | 53 | wszystkie zakończone powodzeniem |
| `Flota.Presentation.Tests` | 71 | wszystkie zakończone powodzeniem |
| `Flota.App.Tests` | 21 | wszystkie zakończone powodzeniem |
| `tools/TestyUi` (przeglądarka) | 37 sprawdzeń | wszystkie zakończone powodzeniem |
