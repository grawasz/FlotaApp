# Pomiar bazowy aplikacji Blazor

Czasy ładowania danych w dotychczasowym rozwiązaniu `C:\Projects\Flota`, stanowiące punkt odniesienia dla mierników z rozdziału 6.2 planu.

## Metoda

| | |
|---|---|
| Narzędzie | `PomiarBazowy` — aplikacja konsolowa w katalogu roboczym `C:\Projects\FlotaPomiar` (poza repozytoriami), korzystająca z kopii `Flota.Data` |
| Zakres | Te same metody `Flota.Data`, w tej samej kolejności i z tymi samymi parametrami co `Index-a.razor` i `Reports.razor`; metody łączące kilka operacji (`TrackingObject.RefreshMessages`, `TrackingMessage.GetListAsync(id, stageType, …)`) odtworzone w narzędziu z rozdzieleniem pobierania danych i geokodowania |
| Poza zakresem | Czas renderowania interfejsu Blazor i transmisji SignalR do przeglądarki (przyczyny P3, P10) |
| Data pomiaru | 2026-09-26 |
| Stanowisko | stacja robocza deweloperska (Windows 11), połączenie z UniAVL (`http://geo.uniavl.pl/AVL/`) i Targeo |
| Konto | konto użytkownika Jurka |
| Powtórzenia | 3 dla startu i raportu trasy; 1 dla raportu floty |
| Wynik źródłowy | `C:\Projects\FlotaPomiar\wyniki\pomiar-20260926-144618.md` |

## Dane konta

| Wielkość | Wartość |
|---|---|
| Obiekty (`api/TrackingObjects`) | 381 |
| Obiekty aktywne | 345 |
| Ostatnie wiadomości (`api/TrackingMessages/Last`) | 335 |
| Obszary | 22 |

## Scenariusz 1 — sekwencja startowa

Od pobrania tokenu do kompletu danych mapy (`Index-a.razor`, `OnAfterRenderAsync(firstRender)`). Mediana z trzech powtórzeń; w nawiasach wartości skrajne.

| Krok | Czas [ms] | Uwagi |
|---|---:|---|
| `api/Login/Ping` (synchronicznie) | 59 (58–129) | |
| `api/Login/Identity` (synchronicznie) | 63 (61–233) | |
| `api/TrackingObjects` | 355 (303–474) | 381 obiektów |
| `api/TrackingMessages/Last` | 1 250 (1 233–1 469) | 335 wiadomości |
| Dopasowanie wiadomości do obiektów | 11 (7–16) | |
| **Geokodowanie Targeo, sekwencyjne** | **15 876 (15 739–16 752)** | 333 punkty, 47–50 ms na punkt |
| `api/TrackingObjects` ponownie (`GetListAsync4Sel`) | 307 (262–317) | powtórzenie pobrania listy obiektów |
| `api/Areas` | 112 (73–230) | 22 obszary |
| **Razem** | **18 611 (17 966–18 820)** | |

Poprzedzające logowanie (`api/Login/Token`): 459 ms.

## Scenariusz 2 — raport trasy

Obiekt 3440, dzień 2026-09-25 (00:00:00–23:59:59), parametry domyślne `Reports.razor`: `timeStop` 00:00:00, `timeIgnitionOff` 00:01:00, `speed` 0, `StageSubtypeBySensor` true. Przebieg odpowiada przyciskowi „Trasa” (`ReportsClick(showRoute: true)`): trasa z etapami, następnie punkty trasy wszystkich etapów.

| Krok | Czas [ms] | Uwagi |
|---|---:|---|
| `…/TrackingMessages/Route` | 1 213 (1 193–1 329) | 20 etapów |
| `…/TrackingMessages?stageType=`, sekwencyjnie po etapie | 3 213 (3 119–3 608) | 20 żądań, 1 735 punktów trasy |
| **Geokodowanie Targeo punktów trasy** | **76 024 (74 768–76 091)** | 1 526 unikalnych punktów, 49–50 ms na punkt |
| **Razem** | **80 430 (79 101–81 032)** | |

## Scenariusz 3 — raport floty

Wszystkie obiekty aktywne (345), okres 2026-08-01 – 2026-08-31 (`Index-a.razor`, `OnClick("reports")` → `FleetReport.GetListAsync`).

| Krok | Czas [ms] | Uwagi |
|---|---:|---|
| `…/FleetReport`, sekwencyjnie po obiekcie | 39 246 | 345 żądań, 334 raporty z danymi; na obiekt: min. 77 ms, śr. 114 ms, maks. 242 ms |

## Wnioski

1. **Geokodowanie jest dominującą przyczyną długiego ładowania (P1).** Odpowiada za 87% czasu startu i 94% czasu raportu trasy. Koszt jest stały — ok. 48 ms na punkt — i powtarza się przy każdym uruchomieniu, ponieważ adresy nie są przechowywane w pamięci podręcznej.
2. **Bez geokodowania start trwa ok. 2,1 s** (mediana różnicy „Razem” − „Geokodowanie”), z czego największy udział ma `api/TrackingMessages/Last` (1,25 s). Wartość ta wyznacza realny cel dla `GET /fleet/snapshot` przy adresach z pamięci podręcznej.
3. **Punkty trasy pobierane etap po etapie (P5)** — 20 sekwencyjnych żądań, łącznie ok. 3,2 s; pobranie równoległe lub jednym żądaniem za cały okres skraca ten krok do czasu najdłuższego żądania.
4. **Raport floty (P5)** — czas jest sumą 345 sekwencyjnych żądań o średnim czasie 114 ms; pobieranie z ograniczoną współbieżnością w Flota.Api skraca go proporcjonalnie do przyjętego stopnia współbieżności (wartość do ustalenia z dostawcą UniAVL).
5. **Lista obiektów pobierana dwukrotnie (P4)** — powtórzenie kosztuje ok. 0,3 s.
6. Usługa Targeo udostępnia operację wsadową `revGeocodeMulti` (tablica punktów → tablica adresów), której `Flota.Data` nie wykorzystuje; jej przydatność zależy od odpowiedzi na K4 (zob. [PYTANIA.md](PYTANIA.md)).
7. Wywołania synchroniczne `Ping` i `Identity` są krótkie (ok. 60 ms każde), lecz przy pierwszym wywołaniu po uruchomieniu procesu trwają dłużej (129 i 233 ms) — koszt zestawienia połączenia bez współdzielonego klienta HTTP (P6).

## Analiza `Addresses.db`

Plik `C:\Projects\Flota\Flota\Addresses.db` (12,7 MB, ostatnia modyfikacja 2022-10-12), analizowany na kopii w trybie tylko do odczytu narzędziem `C:\Projects\FlotaPomiar\src\AnalizaAdresow`.

### Pochodzenie danych

Baza nie zawiera wyników Targeo. Jest pamięcią podręczną odpowiedzi usługi **Nominatim (OpenStreetMap)**, zapisywaną przez `Flota.Data/OpenStreet/OpenStreetAddress.cs` (`Map.GetAsync`) za pośrednictwem `LocalAddress`. Ścieżka ta nie jest używana przez aplikację Blazor. Współrzędne `Lat`/`Lon` w rekordach są nadpisywane współrzędnymi zapytania (pozycją pojazdu), a nie współrzędnymi obiektu zwróconego przez Nominatim — mogą więc służyć jako klucz pamięci podręcznej.

### Struktura

Jedna kolekcja `address`, dokumenty w formacie odpowiedzi Nominatim `reverse?format=jsonv2`:

| Pole | Wypełnienie | Uwagi |
|---|---:|---|
| `Lat`, `Lon` (tekst) | 11 632 | współrzędne zapytania, zwykle 7 miejsc po przecinku (9 945 rekordów) |
| `PlaceId`, `OsmType`, `OsmId`, `Category`, `Type`, `Addresstype`, `PlaceRank`, `Importance`, `Boundingbox` | 11 632 | metadane OSM |
| `DisplayName` | 11 632 | pełny opis tekstowy |
| `Name` | 4 033 | |
| `Licence` | 11 632 | „Data © OpenStreetMap contributors, ODbL 1.0” |
| `Address.Country`, `Address.CountryCode` | 11 632 | |
| `Address.State` | 11 630 | województwo |
| `Address.Postcode` | 11 316 | |
| `Address.Road` | 9 468 | |
| `Address.County` | 8 311 | powiat |
| `Address.Municipality` | 7 839 | gmina |
| `Address.City` / `Town` / `Village` | 3 339 / 4 186 / 3 758 | miejscowość w jednym z trzech pól (łącznie 11 231 rekordów) |
| `Address.Suburb`, `Address.Neighbourhood` | 4 878, 3 197 | |
| numer budynku | 0 | klasa `OpenStreet.Address` nie odwzorowuje pola `house_number` — numery nie zostały zapisane |

### Zawartość

| Wielkość | Wartość |
|---|---:|
| Rekordy | 11 632 |
| Unikalne pary współrzędnych (dokładne) | 11 423 |
| Unikalne klucze po zaokrągleniu do 4 miejsc | 6 469 |
| Klucze 4 miejsc obejmujące więcej niż jeden rekord | 1 269 |
| — w tym z różną ulicą lub miejscowością | 84 (6,6%) |
| — w tym z różną miejscowością | 47 (3,7%) |
| Rekordy z Polski | 11 495 (98,8%) |
| Pozostałe kraje | IT 48, GB 37, FR 20, UA 18, DE 5, AT 4, BE 3, HR 1, SI 1 |

### Ocena przydatności do zasilenia pamięci podręcznej adresów

| Kryterium | Ocena |
|---|---|
| Zgodność źródła | **niezgodna** — w Flota.Api źródłem adresów jest Targeo; Nominatim ma inną strukturę (miejscowość w trzech polach, brak dzielnicy w ujęciu Targeo) i inne nazewnictwo. Mieszanie źródeł powoduje różny zapis adresu dla sąsiednich punktów. |
| Kompletność | niepełna — brak numerów budynków; 19% rekordów bez ulicy |
| Aktualność | dane sprzed ok. 4 lat |
| Licencja | ODbL 1.0 — wymaga przypisania źródła; baza pochodna podlega warunkom ODbL |
| Klucz 4 miejsc (ok. 11 m) | potwierdzony jako rozsądny: w 93,4% kluczy obejmujących kilka punktów adres jest identyczny; różnice dotyczą punktów na granicach ulic i miejscowości |
| Wolumen | 6 469 kluczy — niewielki wobec ok. 1 500 unikalnych punktów w jednym dziennym raporcie trasy jednego obiektu |

**Rekomendacja:** nie zasilać pamięci podręcznej Flota.Api danymi z `Addresses.db` (plan, rozdz. 5.2, pkt 5). Pamięć podręczna budowana wyłącznie z wyników Targeo, z zastrzeżeniem rozstrzygnięcia K4 (dopuszczalność przechowywania wyników Targeo). Dane z `Addresses.db` mogą posłużyć wyłącznie jako zbiór testowy współrzędnych dla testów wydajności geokodowania. Decyzja do potwierdzenia na punkcie kontrolnym 0.
