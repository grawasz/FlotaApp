# Środowisko deweloperskie

Stan stacji roboczej Windows, na której budowane są wersje Windows i Android. Wersja iOS jest budowana w potoku CI (macOS).

## Składniki

| Składnik | Stan | Wymaganie | Ocena |
|---|---|---|---|
| System | Windows 11 Pro 10.0.26200, x64; `LongPathsEnabled = 1` | obsługa długich ścieżek | poprawny |
| .NET SDK | 10.0.400, 10.0.401 | .NET 10 SDK | poprawny |
| Obciążenia .NET | `android` 36.1.2, `ios` 26.5.10301, `maui` 10.0.1, `maui-windows` 10.0.1 | `android`, `ios` | poprawny |
| Visual Studio | 18.10 Insiders: .NET desktop development, .NET MAUI | — | poprawny |
| JDK | Microsoft OpenJDK 21.0.10, Microsoft OpenJDK 25.0.2, Liberica JDK 25.0.2; systemowa zmienna `JAVA_HOME` → JDK 25.0.2 | JDK 17 lub 21 dla .NET for Android | zob. uwaga 1 |
| Android SDK | `C:\Program Files (x86)\Android\android-sdk`: `platforms;android-36`, `platform-tools` 36.0.0, `build-tools;35.0.1`, `cmdline-tools;17.0`, `emulator` 37.1.11, `system-images;android-35;google_apis;x86_64` | — | poprawny |
| Emulator Android | urządzenie AVD `Android_Emulator_35`; akceleracja WHPX dostępna | — | poprawny |
| Szablony Uno | `unoapp`, `unoapp-uitest` zainstalowane | — | poprawny |
| `Uno.Sdk` | 6.7.30 — najnowsza wersja stabilna w NuGet | — | poprawny |
| Edge WebView2 | 153.0.4234.48 | — | poprawny |
| `dotnet-ef` | 10.0.12 | — | poprawny |
| `uno-check` | 1.34.1 (narzędzie globalne; wymaga uprawnień administratora) | — | — |

## Wynik `uno-check`

Polecenie: `uno-check check --non-interactive --target android --target windows`, uruchomione bez uprawnień administratora.

| Kontrola | Wynik |
|---|---|
| OpenJDK | poprawny |
| Visual Studio i obciążenia VS | poprawny |
| Android SDK | poprawny |
| Android Emulator | poprawny |
| .NET SDK | poprawny |
| Windows Long Path | błąd dostępu do rejestru bez uprawnień administratora; odczyt `reg query` potwierdza `LongPathsEnabled = 1` |
| Hyper-V | zalecenie informacyjne; emulator korzysta z akceleracji WHPX |
| Szablony `dotnet new` Uno | poprawny |
| Uno SDK | poprawny |
| Edge WebView2 | poprawny |
| Obciążenia .NET SDK | poprawny |

Brak błędów krytycznych dla Android i Windows.

## Uwagi

1. .NET for Android obsługuje JDK 17 i 21. Ze względu na systemowe `JAVA_HOME` wskazujące JDK 25, budowanie Android korzysta z JDK 21 przez właściwość MSBuild `JavaSdkDirectory` (`C:\Program Files\Microsoft\jdk-21.0.10.7-hotspot`), ustawianą w konfiguracji rozwiązania.
