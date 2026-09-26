# Specyfikacja aplikacji źródłowej (Blazor)

Opis interfejsu i zachowania aplikacji `C:\Projects\Flota\Flota` (strona `/` — `Pages/Index-a.razor`, komponent `Components/Reports.razor`, `Pages/Login.razor`) stanowiący wzorzec parytetu dla aplikacji Uno. Odwołania w formacie `plik:wiersz` dotyczą kodu źródłowego.

## 1. Ogólne

| Element | Wartość |
|---|---|
| Wersja | 0.17.4.1 |
| Krój pisma | Helvetica Neue / Helvetica / Arial, 12 px (`site.css`); motyw Radzen Material 3 |
| Tło strony | `#F0F0F0`; paski narzędzi paneli `#EBEFF1`/`#EBEFF2`; menu raportu `#F0F0F0` |
| Kolor akcentu (etykiety przycisków, nagłówki paneli) | `#1A126C`, nagłówki paneli pogrubione |
| Nagłówek panelu | ikona 16×16 + tekst pogrubiony `#1A126C`; panel zwijany |
| Przycisk wciśnięty | przezroczystość 0,5, obramowanie „inset” 1 px białe |
| Wskaźnik ładowania | `load-big.gif` (start), `load-little.gif` (raport) |
| Komunikat błędu | powiadomienie „Błąd” (w nowej aplikacji bez stosu wywołań — B4) |
| Tabele | gęstość kompaktowa, filtrowanie, sortowanie, zmiana szerokości kolumn, pusty stan „Brak danych” |

## 2. Logowanie

Karta „Logowanie” (70% szerokości, maks. 600 px): pola „Użytkownik”, „Hasło”, przycisk „Zaloguj”. Walidacja: „Wprowadź prawidłowy login”, „Wprowadź hasło”. Błąd logowania: okno „Błąd” — „Wprowadzono błędne hasło lub nazwę użytkownika”, przycisk „Ok”. Wylogowanie: czyszczenie sesji i powrót do logowania.

## 3. Strona główna — układ

- Pasek nagłówka (wysokość 28 px) z przyciskami przełączającymi panele; każdy: ikona 16×16 + etykieta 12 px/500 `#1A126C`. Na wąskim ekranie (szerokość < 1168 px lub orientacja pionowa) — same ikony z podpowiedzią.
- Kolejność: Obiekty (`tracking_objects`), Szczegóły obiektu (`tracking_object`), Mapa (`map`), Obszary (`areas`), Kierowcy (`drivers`) | Zdarzenia (`events`), Wiadomości (`messages`) | Raport trasy (`reports`), Raport floty (`reports_daily`), Ranking Eco-Driving (`reports_eco`), Karty drogowe (`logbook`), Wyposażenie (`equipment`). Separator: pionowa kreska 2 px `darkgray`.
- Stan początkowy: Obiekty i Szczegóły włączone (poza orientacją pionową), Mapa włączona, pozostałe wyłączone.
- Kolumny (siatka 12): lewa 3/12 (Obiekty, Szczegóły, Obszary, Kierowcy), mapa 9/12 lub 4/12 gdy otwarty panel prawy, prawa 5/12 (Zdarzenia, Wiadomości, raporty, karty drogowe, wyposażenie).
- Panel boczny (prawy, 240 px, zwinięty, przycisk „hamburger”): `logo.png` 224×84, „Flota Ⓒ 2023”, u dołu wersja i „Copyright UNI-Net Poland Sp. z o.o Ⓒ 2023”.

## 4. Obiekty

- Pasek: „Odśwież” (`refresh`), „Śledź” (`trace`, przełącznik z wyglądem wciśniętym).
- Tabela (wysokość 640 px lub 300 px gdy widoczne szczegóły/obszary/kierowcy), kolumny:
  1. „✓” — widoczność na mapie; nagłówek zaznacza/odznacza wszystkie; nieaktywne obiekty zablokowane.
  2. „Nazwa” — `TrackingObjectName`.
  3. przycisk statusu: okrąg w kolorze statusu z ikoną typu `to_*.png` (20 px); kliknięcie otwiera obiekt.
  4. „Lokalizacja” — miejscowość lub województwo.
- Domyślne sortowanie: status (W ruchu, Zatrzymany, Brak GPS, Nieaktualny, Nieznany), następnie nazwa.
- Wybór wiersza: zaznaczenie obiektu, wyróżnienie na mapie, wyśrodkowanie; dwuklik — przybliżenie 18.
- Status: Nieznany (brak wiadomości / nieaktywny) → Nieaktualny (> 60 min) → Brak GPS → Zatrzymany (stacyjka wył.) / W ruchu. Kolory: `#008000`, `#FFA500`, `#808080`, `#FF0000`, biały (przezroczysty, 50%).
- Ikony typów 0–22: car, van, truck, tractor_unit, bus, ambulance, police, firetruck, snowplow, hauler, cement_mixer, helicopter, motorbike, bike, horseman, guard, man, tractor, road_roller, portable_radio, mobile_radio, truck_trailer, excavator.

## 5. Szczegóły obiektu

Etykieta (20%) — wartość (78%), kultura pl-PL:

| Etykieta | Wartość | Warunek |
|---|---|---|
| Nazwa: | nazwa | zawsze |
| Rejestracja: | numer rejestracyjny | zawsze |
| Marka: / Model: | | niepuste |
| Prędkość: | `N0 km/h` | jest |
| Prędkość CAN: | `N0 km/h` | jest |
| Obrotomierz: | `N0 obr./min.` | jest |
| Stan licznika: | `N0 km` | jest |
| Poziom paliwa: | `N0 l (P0)` | jest |
| Okolica: | pełny adres | aktywny i adres znany |
| Obszar: | (puste) | zawsze |
| ikona `tracking_mode_public_32x32` | „Tryb publiczny” | zawsze |
| ikona `satelites_32x32` | liczba satelitów | > 0 |
| ikona `ignition_enabled/disabled_32x32` | „Stacyjka” | typ ≠ 19 |
| czujniki widoczne wg `Order` | logiczne: kółko 16 px `#008000`/`#D3D3D3` + nazwa; wartościowe: nazwa + wartość | |
| `calendars_16x16` data `yyyy-MM-dd`, `clock_16x16` godzina `HH:mm` | | zawsze |

## 6. Mapa

- OSM, środek początkowy 52,308479 / 20,162054, powiększenie 6 (maks. 18); widok początkowy użytkownika z ustawień.
- Menu kontekstowe „Widok początkowy”: „Ustaw bieżący”, „Przejdź”, „Usuń”.
- Znacznik obiektu: koło 36 px w kolorze statusu, ikona typu, etykieta stała z nazwą (przezroczystość 0,8); wybrany — 38 px, obramowanie 2 px `darkslategrey`, etykieta `darkred` pogrubiona.
- Okno obiektu: nazwa (`steelblue`), rejestracja (`tomato`); Kierowca; Prędkość; Prędkość maksymalna; Stan paliwa; Włączony zapłon: Tak/Nie; Okolica; data `yyyy-MM-dd HH:mm`.
- Śledzenie: ślad od ostatniej wiadomości − 5 min; linia niebieska `#0000FF`, szerokość 10, przezroczystość 0,6; strzałki kierunku (`arrows-o/arrow_*.png`, 24 px, przezroczystość 0,6), postój — `dot.png` 14 px; strzałki mają okno z danymi punktu.
- Obszary: wielokąty w kolorach z danych (Win32 BGR), podpowiedź z nazwą.
- Aktualizacja pozycji w czasie rzeczywistym (WebSocket UniAVL); scalanie: pozycja zastępowana tylko przy poprawnym GPS.

## 7. Obszary, kierowcy, wyposażenie

- Obszary: kolumna „Nazwa”; wybór przesuwa mapę do obszaru.
- Kierowcy: „Nazwisko”, „Imię”, „Aktywny”.
- Wyposażenie: „Odśwież”, „Eksport” (plik „Wyposażenie yyyyMMdd_HHmm.xlsx”); kolumny: Numer rejestracyjny, Nazwa, Urządzenie, Sonda paliwa, Alko detektor, Rejestrator wideo, Kamera, Monitor.

## 8. Zdarzenia

Wybór obiektów („Wybierz obiekty...”), „Odśwież”; przyciski edycji (Dodaj zdarzenie, Dodaj termin, Modyfikuj, Usuń) nieaktywne. Zakładki: „Zdarzenia” (Obiekt, Nazwa, Czas trwania, Godz. obow. od, Godz. obow. do; „Łącznie: N”), „Terminy” (Obiekt, Nazwa, Termin, Powiadomienie; „Łącznie: N”).

## 9. Raport floty

„Generuj raport”, „Eksport podsumowania”, „Eksport szczegółów”; parametry „Data od”/„Data do” (domyślnie wczoraj 00:00 – teraz), „Pojazdy”. Zakładki „Podsumowanie” i „Szczegóły” (+ kolumna „Data”). Kolumny warunkowe (tylko gdy istnieje wartość): Obiekt, Marka, Stan licznika, Czas jazdy, Czas postoju, Dystans [km] (0.00), Prędkość średnia [km/h] (N0), Prędkość maksymalna [km/h] (N0), Maksymalne obroty [obr./min.], Zużycie paliwa [l] (0.00), Spalanie [l/100 km] (0.00), Spalanie [l/godz.] (0.00), Aktywność: Bieg neutralny, Aktywność: Bieg neutralny w ruchu, Aktywność: Drzwi, Aktywność: Hamulec.

## 10. Karty drogowe

„Pokaż szczegóły”; nieaktywne: Dodaj, Usuń, Wydruk (szablon VAT). „Okres od … do”. Lista: Numer, Data (`yyyy-MM-dd HH:mm`), Numer rejestracyjny, Początkowy stan licznika, Końcowy stan licznika, Przebieg, Kierowca. „Pozycje karty drogowej”: Cel wyjazdu; Wyjazd (Godzina, Adres, GPS, Stan licznika); Dojazd (Godzina, Adres, GPS, Stan licznika); Przebieg; Kierowca.

## 11. Raport trasy

- Pasek: „Generuj raport”, „Generuj raport (trasa)”; odtwarzanie: Pierwszy, Poprzedni, Start, Stop, Następny, Ostatni (krok co 3 s); „Excel” z pozycjami: Eksport podsumowania trasy, Eksport kursów, Eksport punktów trasy, Eksport tankowań, Eksport ubytków paliwa, Eksport zdarzeń, Eksport podsumowania kursów.
- Parametry: „Typowy okres” (Ostatnia godzina — domyślnie, Dzisiaj, Wczoraj, Bieżący tydzień, Poprzedni tydzień, Bieżący miesiąc, Poprzedni miesiąc, Niestandardowy), „Data od”/„Data do” (`yyyy-MM-dd HH:mm`, edycja tylko dla niestandardowego), „Obiekt” („Wybierz obiekt...”).
- Kryteria: „Podział kursów na podtypy”; Postój: Stacyjka — „Czas wyłączenia” (00:01:00, domyślnie włączone), Postój — „Czas postoju” (00:02:00), „Prędkość min.” (5 km/h); przynajmniej jedno z kryteriów włączone. Styl jazdy: Maksymalna prędkość (50), Gwałtowne hamowania (1), Maksymalne obroty (2000), Gwałtowne przyspieszenia (1).
- Zakładki: Trasa (Okres; Podsumowanie: Końcowy stan licznika N0 km, Dystans N1 km, Dystans GPS, Czas jazdy, Czas postoju, Czas holowania, Średnia prędkość N0 km/h, Maks. prędkość N1 km/h, Maks. obroty; Paliwo: Zużycie, Spalanie l/100 km i l/godz., Suma tankowań; Aktywność czujników), Kursy (wybór wielokrotny, „Pokaż na mapie”; Typ, Data początkowa, Data końcowa, Czas trwania, Dystans [km], Średnia prędkość [km/h], Maks. prędkość [km/h], Maks. obroty + kolumny dynamiczne), Punkty trasy (Data, Prędkość [km/h], Obroty, Stan licznika [km], Miasto, Ulica, Stacyjka + czujniki widoczne; > 5000 punktów — komunikat „Informacja”), Paliwo (Poziom początkowy/końcowy, Całkowite zużycie paliwa, Suma tankowań, Średnie spalanie; Tankowania, Ubytki; wykresy ilości paliwa w funkcji czasu i drogi z linią pojemności baku), Zdarzenia (Data, Opis, Miasto, Ulica), Styl jazdy (czasy przekroczeń, liczby zdarzeń, wykres cech 0–100), Podsumowanie kursów (Typ, daty, Czas trwania, Miasto, Ulica — dla postojów, Dystans, prędkości).
- Mapa: ślad wybranych kursów, znacznik obiektu w punkcie bieżącym, strzałki kierunku.

## 12. Błędy aplikacji źródłowej nieprzenoszone

Liczba gwałtownych hamowań wyświetlająca przyspieszenia; nieedytowalne „Podział kursów na podtypy”; niepowiązana lista obszarów w kryteriach; niespójność widoczności zakładki Paliwo; kumulowanie wierszy eksportu podsumowania; brak zabezpieczeń przed brakiem baku; panel Kierowcy widoczny tylko razem z Obszarami; brakujące ścieżki ikon Eco-Driving i Wyposażenie; pusty panel Ranking Eco-Driving.
