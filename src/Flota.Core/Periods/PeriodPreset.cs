namespace Flota.Core.Periods;

/// <summary>Typowe okresy raportu (odpowiednik <c>PeriodTypes</c> z aplikacji Blazor).</summary>
public enum PeriodType
{
    LastHour,
    Today,
    Yesterday,
    CurrentWeek,
    LastWeek,
    CurrentMonth,
    LastMonth,
    NonStandard,
}

/// <summary>Wyznaczanie zakresu dat dla typowych okresów raportu.</summary>
public static class PeriodPreset
{
    public static IReadOnlyList<PeriodType> All { get; } = Enum.GetValues<PeriodType>();

    /// <summary>Nazwa okresu wyświetlana w interfejsie.</summary>
    public static string DisplayName(PeriodType type) => type switch
    {
        PeriodType.LastHour => "Ostatnia godzina",
        PeriodType.Today => "Dzisiaj",
        PeriodType.Yesterday => "Wczoraj",
        PeriodType.CurrentWeek => "Bieżący tydzień",
        PeriodType.LastWeek => "Poprzedni tydzień",
        PeriodType.CurrentMonth => "Bieżący miesiąc",
        PeriodType.LastMonth => "Poprzedni miesiąc",
        PeriodType.NonStandard => "Niestandardowy",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    /// <summary>
    /// Zakres dat dla okresu. Dla <see cref="PeriodType.NonStandard"/> zwraca <paramref name="current"/> bez zmian.
    /// </summary>
    public static (DateTime From, DateTime To) Compute(PeriodType type, DateTime now, (DateTime From, DateTime To) current = default)
    {
        switch (type)
        {
            case PeriodType.LastHour:
                return (now.AddHours(-1), now);
            case PeriodType.Today:
                return (now.Date, EndOfDay(now));
            case PeriodType.Yesterday:
                var y = now.Date.AddDays(-1);
                return (y, EndOfDay(y));
            case PeriodType.CurrentWeek:
                var w = StartOfWeek(now);
                return (w, EndOfDay(w.AddDays(6)));
            case PeriodType.LastWeek:
                var lw = StartOfWeek(now).AddDays(-7);
                return (lw, EndOfDay(lw.AddDays(6)));
            case PeriodType.CurrentMonth:
                var m = new DateTime(now.Year, now.Month, 1);
                return (m, EndOfDay(m.AddMonths(1).AddDays(-1)));
            case PeriodType.LastMonth:
                var lm = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                return (lm, EndOfDay(lm.AddMonths(1).AddDays(-1)));
            case PeriodType.NonStandard:
                return current;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    /// <summary>Poniedziałek tygodnia zawierającego datę, godz. 00:00.</summary>
    public static DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    static DateTime EndOfDay(DateTime date) => date.Date.AddDays(1).AddSeconds(-1);
}
