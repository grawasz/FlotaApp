using System.Globalization;

namespace Flota.App.Services;

/// <summary>Teksty interfejsu z zasobów (<c>Strings/pl-PL/Resources.resw</c>) i formatowanie wartości (pl-PL).</summary>
public static class Loc
{
    public static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");

    static IStringLocalizer? _localizer;

    public static void Initialize(IStringLocalizer localizer) => _localizer = localizer;

    /// <summary>Tekst zasobu; klucz w postaci <c>Nazwa</c> (bez właściwości).</summary>
    public static string Get(string key)
    {
        var value = _localizer?[key];
        return value is null || value.ResourceNotFound ? key : value.Value;
    }

    public static string Format(string key, params object?[] args) => string.Format(Pl, Get(key), args);

    public static string N0(double? v) => v?.ToString("N0", Pl) ?? string.Empty;
    public static string N1(double? v) => v?.ToString("N1", Pl) ?? string.Empty;
    public static string N2(double? v) => v?.ToString("0.00", Pl) ?? string.Empty;
    public static string DateTime(DateTime? v) => v?.ToString("yyyy-MM-dd HH:mm:ss", Pl) ?? string.Empty;
    public static string DateTimeShort(DateTime? v) => v?.ToString("yyyy-MM-dd HH:mm", Pl) ?? string.Empty;
    public static string Date(DateTime? v) => v?.ToString("yyyy-MM-dd", Pl) ?? string.Empty;
    public static string Time(DateTime? v) => v?.ToString("HH:mm", Pl) ?? string.Empty;
    public static string Duration(TimeSpan? v) => v is null ? string.Empty : v.Value.ToString(v.Value.TotalDays >= 1 ? @"d\.hh\:mm\:ss" : @"hh\:mm\:ss", Pl);

    /// <summary>Wartość liczbowa do eksportu (kultura niezmienna).</summary>
    public static string? Raw(double? v) => v?.ToString("R", CultureInfo.InvariantCulture);
    public static string? Raw(int? v) => v?.ToString(CultureInfo.InvariantCulture);
    public static string? Raw(DateTime? v) => v?.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
}
