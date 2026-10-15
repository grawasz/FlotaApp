# Pytania do dostawców UniAVL i Targeo

Zestawienie pytań dotyczących kwestii K1–K4 z rozdziału 12 planu oraz pytań uzupełniających wynikających z pomiaru bazowego ([POMIAR-BAZOWY.md](POMIAR-BAZOWY.md)) i analizy kodu. Przy każdym pytaniu podano kontekst i wpływ odpowiedzi na rozwiązanie. Pytania wysyła Jurek.

## UniAVL

### K1 — szyfrowanie połączeń

**Kontekst.** Aplikacja korzysta z adresu `http://geo.uniavl.pl/AVL/`. Login i hasło są przekazywane w parametrach zapytania `GET api/Login/Token?Login=&Password=`, a token w nagłówku `Authorization` — bez szyfrowania. Strumień pozycji korzysta z `wss://geo.uniavl.pl:44380` (szyfrowany). Notatki projektowe aplikacji Blazor wymieniają adres HTTPS `https://geo.uniavl.pl:44381` jako adres usługi komunikacyjnej (hub SignalR `CommunicationHub`); dostępność REST API pod tym adresem nie została sprawdzona.

1. Czy UniAVL REST API jest dostępne przez HTTPS (np. pod `https://geo.uniavl.pl:44381`)? Jeżeli tak — pod jakim adresem i czy dotychczasowy adres HTTP pozostanie dostępny?
2. Czy możliwe jest pobieranie tokenu metodą `POST` z danymi logowania w treści żądania zamiast w parametrach adresu?
3. Jaki jest czas ważności tokenu i czy istnieje mechanizm jego odnowienia bez ponownego podawania hasła?

**Wpływ:** bezpieczeństwo odcinka Flota.Api → UniAVL (ryzyko R1); sposób przechowywania danych logowania w Flota.Api.

### K2 — punkty końcowe zapisu i funkcje aplikacji WPF

**Kontekst.** Aplikacja Blazor korzysta wyłącznie z odczytu. Aplikacja WPF UniNet.AVL udostępniała dodatkowo edycję szablonów zdarzeń, przypomnień i wyposażenia, raport pracy poza godzinami, żądanie odczytu tachografu oraz wiadomości tekstowe i głosowe (hub SignalR).

1. Czy UniAVL API udostępnia punkty końcowe dodawania, modyfikacji i usuwania: szablonów zdarzeń i przypomnień (`…/EventTemplates`), wyposażenia (`api/Equipment`)? Prosimy o dokumentację (ścieżki, metody, format danych).
2. Czy dostępny jest punkt końcowy raportu pracy poza godzinami?
3. Czy dostępne jest żądanie zdalnego odczytu tachografu i odczyt jego wyniku?
4. Czy wysyłanie wiadomości tekstowych i głosowych do urządzeń i grup jest dostępne przez REST API, czy wyłącznie przez hub SignalR? Jeżeli przez hub — adres, sposób uwierzytelnienia i opis komunikatów.
5. Czy istnieje dokumentacja całego API (np. OpenAPI/Swagger)?

**Wpływ:** zakres fazy 6 planu.

### K3 — strumień pozycji (WebSocket)

**Kontekst.** Logowanie do `wss://geo.uniavl.pl:44380` odbywa się poleceniem `LOGIN <użytkownik> <hasło>`. Aplikacja Blazor przechowuje w tym celu hasło użytkownika w pamięci sesji przeglądarki. Komunikaty strumienia to obiekty JSON pozycji (`TrackingMessage`), przypisywane do obiektów według pola `SenderId` urządzenia; po zamknięciu połączenia aplikacja Blazor łączy się ponownie po 5 s. Bez uwierzytelnienia tokenem lub konta technicznego Flota.Api musiałaby przechowywać hasło użytkownika (w postaci zaszyfrowanej) przez cały czas trwania sesji — do czasu rozstrzygnięcia aktualizacja pozycji odbywa się przez odpytywanie co 10 s. W nowym rozwiązaniu połączenie ze strumieniem utrzymuje serwer Flota.Api i rozsyła zmiany do aplikacji.

1. Czy WebSocket przyjmuje uwierzytelnienie tokenem (tym samym co REST API) zamiast loginu i hasła?
2. Czy możliwe jest konto techniczne, które otrzymuje pozycje wszystkich obiektów firmy? Jeżeli tak — czy strumień zawiera identyfikator obiektu lub urządzenia pozwalający przefiltrować dane według uprawnień poszczególnych użytkowników?
3. Czy istnieją ograniczenia liczby równoczesnych połączeń WebSocket na konto lub na firmę?
4. Jaki jest format komunikatów strumienia i czy dostępna jest jego dokumentacja? Czy serwer wysyła komunikaty podtrzymujące połączenie?
5. Czy po ponownym połączeniu możliwe jest pobranie zdarzeń z okresu przerwy?

**Wpływ:** architektura strumienia pozycji w Flota.Api (ryzyko R2); wariant rezerwowy — osobne połączenie na aktywnego użytkownika.

### Pytania uzupełniające — wydajność i obciążenie

**Kontekst.** Pomiar bazowy: 381 obiektów; `api/TrackingMessages/Last` — ok. 1,25 s; raport floty za miesiąc — 345 sekwencyjnych żądań `POST …/FleetReport`, łącznie ok. 39 s; punkty trasy pobierane osobnym żądaniem dla każdego etapu (20 żądań dla jednego dnia jednego obiektu). W Flota.Api planowane jest pobieranie równoległe.

1. Jaka liczba równoczesnych żądań z jednego konta lub adresu IP jest dopuszczalna? Czy obowiązują limity liczby żądań w czasie?
2. Czy `POST api/TrackingObjects/{id}/FleetReport` wyłącznie zwraca raport, czy zapisuje go lub wywołuje inne skutki po stronie UniAVL?
3. Czy istnieje punkt końcowy raportu floty dla wielu obiektów w jednym żądaniu?
4. Czy `api/TrackingObjects/{id}/TrackingMessages` obsługuje stronicowanie oraz pobranie punktów trasy za cały okres jednym żądaniem (bez podziału na etapy)?
5. Czy `api/TrackingMessages/Last` obsługuje filtrowanie (np. zmiany od wskazanego czasu) lub warunkowe pobieranie (`ETag`, `If-Modified-Since`)?
6. Czy odpowiedzi API mogą być kompresowane (`gzip`, `br`)?
7. Czy udostępniane jest środowisko testowe lub konto demonstracyjne (potrzebne do testów automatycznych i do przeglądu aplikacji w App Store)?

**Wpływ:** stopień współbieżności w Flota.Api (plan, 6.1, P5), projekt `GET /fleet/snapshot` i punktów końcowych raportów.

## Targeo

### K4 — warunki korzystania z geokodowania

**Kontekst.** Aplikacja wykonuje geokodowanie odwrotne operacją `revGeocode`, osobnym wywołaniem SOAP dla każdego punktu, sekwencyjnie (ok. 48 ms na punkt). Start aplikacji wymaga geokodowania ok. 330 punktów (ok. 16 s), raport trasy jednego obiektu za jeden dzień — ok. 1 500 punktów (ok. 76 s). Adresy nie są przechowywane, więc te same punkty są geokodowane przy każdym otwarciu. W Flota.Api planowana jest wspólna pamięć podręczna adresów dla wszystkich użytkowników, z kluczem o dokładności ok. 11 m.

1. Jaki jest dopuszczalny miesięczny i dzienny limit zapytań geokodowania w ramach obecnej umowy? Jak liczone są zapytania wsadowe (`revGeocodeMulti`) — jako jedno zapytanie czy jako liczba punktów?
2. Czy umowa dopuszcza przechowywanie wyników geokodowania (pamięć podręczna)? Jeżeli tak — na jak długo i czy wyniki mogą być współdzielone między użytkownikami tej samej firmy?
3. Jaka liczba równoczesnych zapytań jest dopuszczalna?
4. Usługa udostępnia operację `revGeocodeMulti`. Jaka jest maksymalna liczba punktów w jednym wywołaniu i czy jest ona zalecana do geokodowania wsadowego?
5. Czy jedna sesja (`secureLogin`) może być używana długotrwale i równolegle przez wiele wątków? Jaki jest czas ważności sesji?
6. Czy usługa jest dostępna przez HTTPS? Obecnie używany jest adres `http://mapa.targeo.pl/`.

**Wpływ:** projekt `GeocodingService` w Flota.Api (plan, 5.2): czas przechowywania wpisów, współbieżność, wykorzystanie zapytań wsadowych (ryzyko R3); wariant rezerwowy — własny serwer Nominatim.
