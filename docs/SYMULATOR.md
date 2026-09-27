# Symulator UniAVL

`tools/Flota.UniAvlSim` — lokalna usługa odtwarzająca wywołania UniAVL REST API używane przez Flota.Api. Umożliwia uruchomienie Flota.Api i aplikacji bez konta UniAVL: testy integracyjne, zrzuty ekranu, demonstracje.

## Dane

Flota fikcyjna (12 obiektów, 8 kierowców, 3 obszary); nazwy, numery rejestracyjne i osoby są zmyślone. Stan obiektów jest funkcją bieżącego czasu — te same zapytanie o ten sam okres daje ten sam wynik.

| Zachowanie | Obiekty | Stan na liście obiektów |
|---|---|---|
| jazda po zamkniętej pętli: 40 min jazdy, 15 min postoju z wyłączoną stacyjką | 101, 102, 103, 105, 106, 110, 111 | W ruchu / Zatrzymany |
| postój, wiadomość co 5 min | 104, 109 | Zatrzymany |
| brak poprawnej pozycji GPS | 107 | Brak GPS |
| brak wiadomości od 3 h 15 min | 108 | Nieaktualny |
| obiekt bez urządzenia | 112 | Nieznany |

| Dane | Model |
|---|---|
| Trasa, kursy | kursy wyznaczone z cyklu jazdy i postoju; prędkość i zużycie paliwa według typu obiektu |
| Punkty trasy, ślad | wiadomość co 60 s (obiekty na postoju co 5 min); czujniki „Stacyjka”, „Drzwi”, „Temp. zew. [C]” |
| Paliwo | tankowanie do 90% pojemności codziennie o 6:00; poziom maleje z przejechaną drogą |
| Zdarzenia | wyłączenie stacyjki i otwarcie drzwi na początku postoju, tankowanie, przekroczenie prędkości |
| Raport floty, Eco-Driving | wartości dzienne z tego samego modelu ruchu |
| Karty drogowe | obiekty 101 i 102: kursy między 8:00 a 16:00 |

Geokodowanie (Targeo) nie jest symulowane — adresy pozostają puste.

## Uruchomienie

```powershell
./tools/Start-Symulacja.ps1
```

Skrypt uruchamia symulator (`http://localhost:5390/AVL/`) oraz Flota.Api (`http://localhost:5294/`) skierowane do symulatora, z osobną bazą SQLite w katalogu tymczasowym i wyłączonym geokodowaniem Targeo (bez zapytań do usługi zewnętrznej). Logowanie w aplikacji: login `demo`, hasło `demo`. Opcja `-WebApp <katalog>` udostępnia dodatkowo aplikację przeglądarkową z katalogu publikacji (`wwwroot`).

Zatrzymanie symulatora unieważnia wydane tokeny UniAVL — Flota.Api zgłasza wygaśnięcie sesji, jak przy rzeczywistej usłudze.

## Testy

`SimulatorFixture` w `tests/Flota.Api.Tests` uruchamia symulator i Flota.Api na serwerach testowych w pamięci z zatrzymanym zegarem symulatora. Testy `ApiEndpointsTests` (wszystkie punkty końcowe) i `FleetHubTests` (hub pozycji) korzystają z tego układu.
