using System.Globalization;
using Flota.Core.Contracts;

namespace Flota.Core.Geo;

/// <summary>Konwersje danych geometrycznych i kolorów UniAVL.</summary>
public static class GeoConversions
{
    /// <summary>Kolor Win32 (0x00BBGGRR) → <c>#RRGGBB</c> (odpowiednik <c>ColorTranslator.FromWin32</c>).</summary>
    public static string Win32ToHex(int value)
    {
        var r = value & 0xFF;
        var g = (value >> 8) & 0xFF;
        var b = (value >> 16) & 0xFF;
        return string.Create(CultureInfo.InvariantCulture, $"#{r:X2}{g:X2}{b:X2}");
    }

    /// <summary>
    /// Obrys WKT <c>POLYGON ((lon lat, lon lat, ...))</c> → lista punktów. Separator dziesiętny — kropka.
    /// Punkty niepoprawne są pomijane.
    /// </summary>
    public static IReadOnlyList<GeoPoint> ParseWktPolygon(string? wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt)) return [];
        var start = wkt.IndexOf("((", StringComparison.Ordinal);
        var end = wkt.LastIndexOf("))", StringComparison.Ordinal);
        var body = start >= 0 && end > start ? wkt[(start + 2)..end] : wkt;
        var points = new List<GeoPoint>();
        foreach (var part in body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var xy = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (xy.Length == 2
                && double.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
                && double.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
            {
                points.Add(new GeoPoint(lat, lon));
            }
        }
        return points;
    }

    /// <summary>Klucz pamięci podręcznej adresów: współrzędne zaokrąglone do 4 miejsc (ok. 11 m).</summary>
    public static (int Lat, int Lon) AddressKey(double latitude, double longitude) =>
        ((int)Math.Round(latitude * 10_000, MidpointRounding.AwayFromZero),
         (int)Math.Round(longitude * 10_000, MidpointRounding.AwayFromZero));
}
