namespace Flota.Core.Reports;

/// <summary>Reguły obliczeniowe raportów przeniesione z <c>Flota.Data</c>.</summary>
public static class ReportRules
{
    /// <summary>Nazwy cech stylu jazdy (<c>DrivingStyleFeatureTypes</c>).</summary>
    public static string DrivingFeatureName(int featureType) => featureType switch
    {
        0 => "Przekroczenie prędkości",
        1 => "Przekroczenie obrotów",
        2 => "Gwałtowne hamowanie",
        3 => "Gwałtowne przyspieszenie",
        _ => string.Empty,
    };

    /// <summary>
    /// Wartość cechy stylu jazdy prezentowana jako ocena 0–100
    /// (odpowiednik <c>DrivingStyleFeature.Value</c>): 1 → 1; &gt; 1 → 0; pozostałe → (1 − wartość)·100.
    /// </summary>
    public static double? DrivingFeatureScore(double? raw) => raw switch
    {
        null => null,
        1 => 1,
        > 1 => 0,
        _ => Math.Round((1 - raw.Value) * 100, 0),
    };

    /// <summary>Spalanie [l/100 km]; <c>null</c> przy braku danych lub zerowym dystansie.</summary>
    public static double? ConsumptionPer100Km(double? fuelUsed, double? distance) =>
        fuelUsed is null || distance is null or <= 0 ? null : fuelUsed / distance * 100;

    /// <summary>Spalanie [l/godz.]; <c>null</c> przy braku danych lub zerowym czasie jazdy.</summary>
    public static double? ConsumptionPerHour(double? fuelUsed, TimeSpan rideTime) =>
        fuelUsed is null || rideTime.TotalHours <= 0 ? null : fuelUsed / rideTime.TotalHours;

    /// <summary>Poziom paliwa [l] z ułamka pojemności zbiornika.</summary>
    public static double? FuelLevel(double? fraction, int? tankCapacity) =>
        fraction is null || tankCapacity is null ? null : fraction * tankCapacity;

    /// <summary>Czas w formacie „gg:mm:ss” (godziny mogą przekraczać 24; odpowiednik formatu raportu stylu jazdy bez dni).</summary>
    public static string FormatDuration(TimeSpan value) =>
        $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";

    /// <summary>Nazwa typu zdarzenia (<c>EventTypes</c>).</summary>
    public static string EventTypeName(int eventType) => eventType switch
    {
        0 => "Własne",
        1 => "Drzwi",
        2 => "Alkotest",
        3 => "Paliwo",
        4 => "Obszar",
        5 => "Stacyjka",
        6 => "Prędkość",
        7 => "Przypomnienie",
        8 => "Przypomnienie (przebieg)",
        _ => string.Empty,
    };

    /// <summary>Typ zdarzenia „przypomnienie” — szablony wyświetlane na zakładce „Terminy”.</summary>
    public const int ReminderEventType = 7;
}
