namespace Flota.Presentation;

/// <summary>Motyw interfejsu wybrany przez użytkownika.</summary>
public enum ThemeChoice
{
    /// <summary>Jasny — domyślny, jak w aplikacji Blazor.</summary>
    Light,
    Dark,
    /// <summary>Zgodny z ustawieniem systemu.</summary>
    System,
}

/// <summary>Zapis wyboru motywu w ustawieniach urządzenia.</summary>
public static class ThemeChoices
{
    public static IReadOnlyList<ThemeChoice> All { get; } = Enum.GetValues<ThemeChoice>();

    /// <summary>Wartość zapisana w ustawieniach; wartość nieznana lub brak — motyw jasny.</summary>
    public static ThemeChoice Parse(string? value) =>
        Enum.TryParse<ThemeChoice>(value, ignoreCase: false, out var choice) && Enum.IsDefined(choice) ? choice : ThemeChoice.Light;

    /// <summary>Klucz tekstu nazwy motywu w zasobach.</summary>
    public static string TextKey(ThemeChoice choice) => "Theme" + choice;
}
