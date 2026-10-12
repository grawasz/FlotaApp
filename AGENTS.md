# FlotaUno — zasady pracy

Aplikacja kliencka systemu Flota na Uno Platform (Android, iOS, Windows, przeglądarka — WebAssembly) oraz usługa pośrednicząca Flota.Api. Zakres, architektura i kolejność zadań: `docs/Plan-migracji-Uno.md`.

## Źródła

- `C:\Projects\Flota` — dotychczasowe rozwiązanie (Blazor Server, `Flota.Data`) oraz kod aplikacji WPF (`UniNet.AVL.*`). **Wyłącznie do odczytu** — żadnych zmian, zatwierdzeń ani plików tymczasowych w tym katalogu.
- Główna logika interfejsu Blazor: `Flota/Pages/Index-a.razor` (strona `/`) i `Flota/Components/Reports.razor`. Pliki `Index.razor`, `Index-b.razor`, `*.bak`, `* — kopia`, `florea.old`, `FloAPI-full` są nieaktualne.
- Zasoby graficzne: `Flota/wwwroot/images`.

## Sposób pracy

- Zadania wykonywane w kolejności z rozdziału 8 planu; identyfikator zadania (`Tf.n`) w komunikacie każdego zatwierdzenia.
- Zadanie jest zakończone dopiero po pomyślnej weryfikacji opisanej w planie; wynik zapisywany w `docs/POSTEP.md`.
- Na punktach kontrolnych (⏸) — zatrzymanie i przedstawienie wyniku do akceptacji.
- Brak rozstrzygnięcia kwestii K1–K9 z rozdziału 12 planu: zgłosić i zatrzymać zadanie zależne; nie przyjmować założeń.
- Nie wypełniać dokumentów ani konfiguracji zmyślonymi danymi (daty, wersje, identyfikatory, adresy) — brakujące wartości zostawić puste lub zapytać.
- Publikacja w sklepach, wysyłka buildów do testerów, wdrożenie produkcyjne i wysyłanie jakichkolwiek wiadomości wykonuje Jurek.

## Bezpieczeństwo

- Żadnych haseł, tokenów ani kluczy w kodzie i repozytorium. Konfiguracja deweloperska: `dotnet user-secrets`; CI: sekrety GitHub.
- Dane uwierzytelniające Targeo i UniAVL wyłącznie w Flota.Api — nigdy w `Flota.App`.
- Hasło użytkownika nie jest przechowywane na urządzeniu; tokeny w `PasswordVault`.
- Dane uwierzytelniające zapisane w kodzie `C:\Projects\Flota` nie są kopiowane ani cytowane.

## Konwencje kodu

- .NET 10, `Uno.Sdk` w wersji z `global.json`, nullable włączone, `async Task` zamiast `async void` (poza obsługą zdarzeń XAML), `CancellationToken` w operacjach sieciowych.
- MVVM: `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`).
- JSON: `System.Text.Json` z generatorami źródłowymi (`JsonSerializerContext`), bez refleksji.
- HTTP: `IHttpClientFactory`, Polly; brak tworzenia `HttpClient` na żądanie.
- Reguły biznesowe (statusy obiektów, klasyfikacja urządzeń, obliczenia paliwa, formatowanie adresów) w `Flota.Core`, pokryte testami.
- Kolory wyłącznie przez zasoby z `Styles/ColorPaletteOverride.xaml`; brak wartości kolorów w widokach.
- Nazwy i teksty interfejsu po polsku, w rejestrze profesjonalnym; teksty w plikach zasobów (`.resw`).

## Dokumentacja

- Dokumentacja w `docs/`, po polsku, rejestr profesjonalny, opis wyłącznie stanu bieżącego (bez historii zmian dokumentu).
- Diagramy: SVG w konwencji C4 (architektura), w katalogu `docs/svg/`.

## Polecenia

```powershell
dotnet build FlotaUno.slnx
dotnet test FlotaUno.slnx
dotnet build src/Flota.App -f net10.0-windows10.0.26100
dotnet build src/Flota.App -f net10.0-android
dotnet run --project src/Flota.Api
docker compose up
```

Wersja iOS jest budowana wyłącznie w potoku CI (macOS).
