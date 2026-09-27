using Windows.Storage;

namespace Flota.App.Services;

/// <summary>
/// Motyw aplikacji w ustawieniach lokalnych urządzenia. Motyw ustawiany przy starcie aplikacji
/// (<see cref="Application.RequestedTheme"/>) — spójnie dla wszystkich widoków, okien podręcznych i zasobów pobieranych w kodzie.
/// </summary>
public static class ThemeSettings
{
    const string Key = "Theme";

    public static ThemeChoice Load()
    {
        try
        {
            return ThemeChoices.Parse(ApplicationData.Current.LocalSettings.Values[Key] as string);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Ustawienia niedostępne — motyw domyślny
            return ThemeChoice.Light;
        }
    }

    public static void Save(ThemeChoice choice)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[Key] = choice.ToString();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Ustawienia niedostępne — wybór obowiązuje do zamknięcia aplikacji
        }
    }

    /// <summary>Motyw aplikacji; <c>null</c> — zgodny z systemem.</summary>
    public static ApplicationTheme? ToApplicationTheme(ThemeChoice choice) => choice switch
    {
        ThemeChoice.Dark => ApplicationTheme.Dark,
        ThemeChoice.System => null,
        _ => ApplicationTheme.Light,
    };
}
