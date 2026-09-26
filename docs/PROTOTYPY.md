# Prototypy

## T0.4 — Mapsui (`spikes/MapsuiSpike`)

### Zakres

| | |
|---|---|
| Biblioteka | Mapsui 5.1.0 (`Mapsui.Uno.WinUI`), Uno.Sdk 6.7.30, .NET 10 |
| Scena | kafelki OpenStreetMap; 500 markerów (symbol trójkąta obracany o kurs, kolory statusów); linia trasy 10 000 punktów; obszar ok. 60 × 40 km, cała scena widoczna na ekranie |
| Aktualizacja | co 1 s: zmiana położenia i kursu wszystkich 500 markerów, losowa zmiana koloru statusu 2% markerów, `MemoryLayer.FeaturesWereModified()` + `Map.RefreshGraphics()` |
| Konfiguracja | Release; pomiar automatyczny po uruchomieniu, fazy po 15 s, po 5 s na pobranie kafelków |
| Mierniki | **odświeżenia mapy/s** — liczba narysowanych klatek mapy (`Map.Performance.Count`) na sekundę; **czas rysowania** — czas wykonania rysowania Mapsui (`Map.Performance`); **klatki interfejsu/s** — zdarzenia `CompositionTarget.Rendering` |

W fazach z ruchem mapy żądanie przesunięcia lub zmiany powiększenia jest wysyłane w każdej klatce interfejsu (ok. 50/s). Liczba odświeżeń mapy na sekundę jest więc miarą płynności odczuwanej przez użytkownika. Czas rysowania jest porównywalny wyłącznie w obrębie jednego wariantu renderowania: w rendererze Skia (Android, Skia Desktop) obejmuje zapis poleceń rysowania, a w wariancie WinAppSDK pełne rastrowanie na CPU.

### Stanowiska

| Wariant | Renderowanie mapy | Stanowisko |
|---|---|---|
| Windows — WinAppSDK (`net10.0-windows10.0.26100`, wariant D2) | `SKXamlCanvas`, rastrowanie na CPU | Windows 11, 14 rdzeni |
| Windows — Uno Skia Desktop (`net10.0-desktop`, wariant porównawczy) | renderer Skia Uno, GPU | ten sam komputer |
| Android — emulator (`net10.0-android`) | renderer Skia Uno, GPU emulowane (OpenGL ES przez translację na Intel Graphics hosta) | Android 15 (API 35), x86_64, 4 rdzenie, 1080 × 1920 |

### Wyniki

Odświeżenia mapy na sekundę (czas rysowania śr. / maks. w ms):

| Faza | WinAppSDK | Skia Desktop | Android (emulator) |
|---|---|---|---|
| F1 — mapa nieruchoma, aktualizacja co 1 s | 0,9 (31,5 / 40,1) | 0,9 (1,8 / 11,0) | 0,9 (4,5 / 7,6) |
| F2 — przesuwanie ciągłe, wszystkie warstwy | **21,9** (29,0 / 37,5) | **38,1** (1,6 / 5,9) | **18,5** (7,1 / 70,4) |
| F3 — zmiana powiększenia, wszystkie warstwy | 21,6 (29,2 / 64,6) | 38,1 (1,4 / 4,1) | 18,3 (7,4 / 43,6) |
| F4 — przesuwanie: kafelki + linia | 24,3 (27,9 / 42,5) | 38,3 (1,1 / 2,2) | 23,4 (5,2 / 32,6) |
| F5 — przesuwanie: kafelki + markery | 49,7 (9,0 / 15,3) | 40,0 (1,0 / 2,0) | 37,0 (3,8 / 14,3) |
| F6 — przesuwanie: same kafelki | 50,5 (7,1 / 11,7) | 40,7 (0,2 / 1,3) | 41,6 (0,6 / 11,0) |
| F7 — przesuwanie, wszystkie warstwy, **linia uproszczona** | **32,2** (12,4 / 17,9) | **40,0** (1,0 / 2,2) | **35,9** (4,8 / 21,3) |

Klatki interfejsu w fazach z ruchem: WinAppSDK 50–54/s, Skia Desktop 40–50/s, emulator Android 18–42/s.

| Wielkość | WinAppSDK | Skia Desktop | Android (emulator) |
|---|---:|---:|---:|
| Budowa mapy i warstw | 41–248 ms | 50 ms | 542 ms |
| Aktualizacja danych 500 markerów | 0,15 ms | 0,13 ms | 0,26 ms |
| Uproszczenie linii 10 000 punktów (Douglas-Peucker, tolerancja 0,5 px) | 9,2 ms → 1 046 pkt | 11,3 ms → 763 pkt | 41,4 ms → 467 pkt |

W wariancie WinAppSDK czas rysowania różnił się między uruchomieniami (F2: 29,0–50,4 ms w trzech przebiegach); tabela zawiera przebieg ostatni. Wyniki pozostałych przebiegów prowadzą do tych samych wniosków.

### Wnioski

1. **Mapsui spełnia wymagania funkcjonalne** na Windows i Android: obracane symbole markerów, kolory statusów, linia trasy, kafelki OSM, aktualizacja 500 obiektów co 1 s (przygotowanie danych poniżej 1 ms). Poprawność wyświetlania sprawdzona zrzutem ekranu.
2. **Długa linia trasy jest głównym kosztem rysowania.** Linia z 10 000 punktów obniża płynność przesuwania w wariancie WinAppSDK z ok. 50 do ok. 22 odświeżeń/s; w teście ta sama linia była też najcięższą warstwą na emulatorze. **Upraszczanie linii zależnie od powiększenia jest wymagane** (plan, R4): po uproszczeniu do ok. 1 000 punktów wariant WinAppSDK osiąga ok. 32 odświeżenia/s, a emulator ok. 36/s. Uproszczenie należy wykonywać po stronie Flota.Api lub w tle, przy zmianie poziomu powiększenia.
3. **500 markerów nie stanowi problemu wydajnościowego** — koszt ok. 2 ms na klatkę w wariancie WinAppSDK.
4. **Wariant WinAppSDK (D2) renderuje mapę na CPU** (`SKXamlCanvas`); przy tej samej scenie wariant Uno Skia Desktop odświeża mapę ok. 1,7 raza częściej (38 wobec 22/s przy pełnej linii). Mapsui 5.1 nie udostępnia w kontrolce WinUI przełącznika renderowania GPU. Do rozważenia na punkcie kontrolnym 0: pozostawienie wariantu WinAppSDK (z uproszczeniem linii — ok. 32/s) albo zmiana decyzji D2 na wariant Skia Desktop dla Windows.
5. **Emulator nie jest miarodajny dla płynności na telefonach** — renderuje przez translację OpenGL ES i ogranicza liczbę klatek interfejsu także przy samych kafelkach (ok. 42/s). Cel 60 kl./s z rozdziału 6.2 wymaga pomiaru na fizycznym telefonie klasy średniej (prototyp jest gotowy do instalacji).

### Ustalenia techniczne dla fazy 1

| Ustalenie | Działanie |
|---|---|
| Szablon `unoapp` nie dodaje uprawnienia `android.permission.INTERNET` | Uprawnienie w `Platforms/Android/AndroidManifest.xml` |
| W konfiguracji Release .NET SDK 10.0.4xx dopisuje do `RuntimeIdentifiers` projektu Android identyfikator hosta (`win-x64`) z powodu `PublishTrimmed`; pakiet `Microsoft.NETCore.App.Runtime.Mono.win-x64` dla .NET 10 nie istnieje, co przerywa przywracanie pakietów | `UseDefaultPublishRuntimeIdentifier=false` dla celu Android |
| Pomiar `Map.Performance` jest domyślnie aktywny tylko w konfiguracji Debug | `Performance.IsActive = ActiveMode.Yes` w trybie diagnostycznym |
| Po zmianie manifestu kompilacja przyrostowa Release Android dała pakiet kończący się błędem `UnsatisfiedLinkError` przy starcie | Po zmianach manifestu: czyszczenie `obj`/`bin` celu Android i ponowna instalacja |
| Budowanie Android wymaga JDK 21 (systemowe `JAVA_HOME` wskazuje JDK 25) | `JavaSdkDirectory` w konfiguracji rozwiązania |

### Uruchomienie

```powershell
dotnet build spikes/MapsuiSpike/MapsuiSpike -f net10.0-windows10.0.26100 -c Release
dotnet build spikes/MapsuiSpike/MapsuiSpike -f net10.0-android -c Release -t:Install -p:JavaSdkDirectory="C:\Program Files\Microsoft\jdk-21.0.10.7-hotspot"
dotnet build spikes/MapsuiSpike/MapsuiSpike -f net10.0-desktop -c Release -p:TargetFrameworks=net10.0-desktop
```

Wynik: plik `MapsuiSpike-wynik.txt` w katalogu tymczasowym (Windows: `%TEMP%`, Android: katalog `cache` aplikacji) oraz wpisy `MAPSUISPIKE` w konsoli i logcat.

## T0.5 — DataGrid (`spikes/DataGridSpike`)

### Zakres

| | |
|---|---|
| Kontrolka | WinAppSDK: `CommunityToolkit.WinUI.UI.Controls.DataGrid` 7.1.2; Uno Skia: `Uno.CommunityToolkit.WinUI.UI.Controls.DataGrid` 7.1.206 (port zbudowany dla Uno 5 / .NET 7) |
| Dane | 10 000 wierszy punktów trasy; 7 kolumn stałych jak w `Reports.razor` (`GetWaypoints`: Data, Prędkość, Obroty, Stan licznika, Miasto, Ulica, Stacyjka) oraz 12 kolumn czujników tworzonych dynamicznie; wartości sformatowane (kultura pl-PL) |
| Kolumny dynamiczne | `DataGridTextColumn` z wiązaniem `Cells[i]` tworzone w kodzie; zmiana zestawu czujników (12 → 8) jak przy zmianie obiektu w raporcie |
| Przewijanie | wzorzec automatyzacji `IScrollProvider` (ta sama ścieżka co pasek przewijania), jedno przewinięcie na klatkę interfejsu, 10 s; liczba wczytanych wierszy ze zdarzenia `LoadingRow` |
| Weryfikacja wizualna | zrzuty obszaru tabeli (`RenderTargetBitmap`) |
| Konfiguracja | Release, Windows 11, 14 rdzeni |

### Wyniki

| Pomiar | WinAppSDK (D2) | Uno Skia Desktop |
|---|---|---|
| Generowanie 10 000 wierszy | 59 ms | 44 ms |
| Budowa 19 kolumn | 42 ms | 16 ms |
| Pierwsze wyświetlenie 10 000 wierszy | 690 ms | 788 ms |
| Pamięć robocza: przed / po wyświetleniu | 130 / 208 MB | 166 / 252 MB |
| Przewijanie wierszami | 37,2 kl./s; 37 wierszy/s | 32,4 kl./s; 15 wierszy/s |
| Przewijanie stronami | 9,1 kl./s; 236 wierszy/s (ok. 110 ms na stronę) | **brak postępu** (0 wczytanych wierszy) |
| Sortowanie 10 000 wierszy + ponowne wyświetlenie | 5 ms + 664 ms | 3 ms + **brak wyświetlenia** (limit 5 s) |
| Zmiana zestawu kolumn (12 → 8) + wyświetlenie | 656 ms | **brak wyświetlenia** (limit 5 s), następnie `InvalidOperationException` |
| Przewijanie wierszami po zmianie kolumn (15 kolumn) | 45,9 kl./s | nie wykonano |
| Pamięć robocza na końcu | 322 MB | — |

Zrzuty po pierwszym wyświetleniu potwierdzają w obu wariantach poprawne dane, nagłówki kolumn dynamicznych i formatowanie liczb.

### Wnioski

1. **WinAppSDK (wariant D2): `DataGrid` z Windows Community Toolkit 7.1.2 spełnia wymagania funkcjonalne** — 10 000 wierszy z wirtualizacją, dynamiczne kolumny czujników, sortowanie (własna obsługa zdarzenia `Sorting`), zmiana zestawu kolumn.
2. **Stały koszt ok. 0,7 s** przy każdym przypisaniu źródła danych (pierwsze wyświetlenie, sortowanie, zmiana kolumn) oraz ok. 110 ms na przewinięcie o stronę przy 19 kolumnach. Akceptowalne dla raportu, ale zauważalne; ograniczenie przez sortowanie w miejscu (bez ponownego przypisania źródła) i stronicowanie punktów trasy z Flota.Api (plan, `GET /objects/{id}/waypoints` ze stronicowaniem).
3. **Utrzymanie komponentu:** `DataGrid` nie został przeniesiony do Windows Community Toolkit 8 (pakiet `CommunityToolkit.WinUI.Controls.DataGrid` nie istnieje w NuGet); wersja 7.1.2 jest ostatnią dla WinAppSDK.
4. **Uno Skia: port `DataGrid` 7.1.206 nie działa poprawnie z Uno 6.7** — po przewinięciu o stronę przestaje wczytywać wiersze, nie wyświetla nowego źródła danych i zgłasza wyjątek. Ryzyko R5 zrealizowane dla wariantu Skia. Jeżeli na punkcie kontrolnym 0 zostanie wybrany wariant Skia dla Windows (zob. T0.4, wniosek 4), tabele wymagają wariantu rezerwowego z R5: `ItemsRepeater` z własnym nagłówkiem kolumn lub komponent komercyjny — do sprawdzenia osobnym prototypem.
5. Na telefonach tabela nie jest używana (D9 — listy kart), więc wynik wariantu Skia nie dotyczy Android i iOS.

### Uruchomienie

```powershell
dotnet build spikes/DataGridSpike/DataGridSpike -f net10.0-windows10.0.26100 -c Release
dotnet build spikes/DataGridSpike/DataGridSpike -f net10.0-desktop -c Release
```

Wynik: `%TEMP%\DataGridSpike-wynik.txt`, zrzuty `%TEMP%\DataGridSpike-*.bmp`, wpisy `DATAGRIDSPIKE` w konsoli.
