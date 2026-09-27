namespace Flota.UniAvlSim;

/// <summary>
/// Ruch obiektów: deterministyczna funkcja czasu (te same dane przy każdym zapytaniu o ten sam okres).
/// Obiekt „jeżdżący” powtarza cykl 40 min jazdy po zamkniętej pętli i 15 min postoju z wyłączoną stacyjką.
/// </summary>
internal static class SimMotion
{
    public const double CycleMinutes = 55;
    public const double DriveMinutes = 40;

    /// <summary>Początek historii symulacji; licznik kilometrów liczony od tej chwili.</summary>
    public static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>Opóźnienie ostatniej wiadomości obiektu bez łączności.</summary>
    public static readonly TimeSpan OfflineFor = TimeSpan.FromMinutes(195);

    /// <summary>Godzina dziennego tankowania do 90% pojemności zbiornika.</summary>
    public static readonly TimeSpan RefuelHour = TimeSpan.FromHours(6);

    static double CyclePosition(SimObject o, DateTime t)
    {
        var m = (t - Epoch).TotalMinutes + o.PhaseMinutes;
        return m - Math.Floor(m / CycleMinutes) * CycleMinutes;
    }

    public static bool IsDriving(SimObject o, DateTime t) =>
        o.Behavior == SimBehavior.Driving && CyclePosition(o, t) < DriveMinutes;

    public static bool IsEngineOn(SimObject o, DateTime t) =>
        o.Behavior == SimBehavior.NoGps || IsDriving(o, t);

    /// <summary>Minuty jazdy od początku historii do chwili <paramref name="t"/>.</summary>
    static double DrivingMinutes(SimObject o, DateTime t)
    {
        if (o.Behavior != SimBehavior.Driving) return 0;
        var m = (t - Epoch).TotalMinutes + o.PhaseMinutes;
        var cycles = Math.Floor(m / CycleMinutes);
        return cycles * DriveMinutes + Math.Min(m - cycles * CycleMinutes, DriveMinutes);
    }

    /// <summary>Droga [km] od początku historii.</summary>
    public static double Km(SimObject o, DateTime t) => DrivingMinutes(o, t) / 60 * o.SpeedKmh;

    public static double Km(SimObject o, DateTime from, DateTime to) => Math.Max(0, Km(o, to) - Km(o, from));

    public static double Odometer(SimObject o, DateTime t) => 40_000 + o.Id % 100 * 7_321 + Km(o, t);

    /// <summary>Chwile przełączenia jazda/postój w przedziale (bez krańców).</summary>
    public static IEnumerable<DateTime> Switches(SimObject o, DateTime from, DateTime to)
    {
        if (o.Behavior != SimBehavior.Driving || to <= from) yield break;
        var pos = CyclePosition(o, from);
        var next = from.AddMinutes(pos < DriveMinutes ? DriveMinutes - pos : CycleMinutes - pos);
        var driving = pos < DriveMinutes;
        while (next < to)
        {
            yield return next;
            next = next.AddMinutes(driving ? CycleMinutes - DriveMinutes : DriveMinutes);
            driving = !driving;
        }
    }

    /// <summary>Wierzchołki pętli trasy: nieregularny ośmiokąt wokół punktu bazowego.</summary>
    static (double Lat, double Lon)[] Loop(SimObject o)
    {
        var points = new (double, double)[8];
        var kmPerDegLon = 111.32 * Math.Cos(o.Latitude * Math.PI / 180);
        for (var k = 0; k < points.Length; k++)
        {
            var angle = k * Math.PI / 4;
            var r = o.RadiusKm * (0.65 + 0.35 * Noise(o.Id * 31 + k));
            points[k] = (o.Latitude + r * Math.Cos(angle) / 111.32, o.Longitude + r * Math.Sin(angle) / kmPerDegLon);
        }
        return points;
    }

    /// <summary>Pozycja i azymut (0° — północ, zgodnie z ruchem wskazówek zegara).</summary>
    public static (double Lat, double Lon, double Azimuth) Position(SimObject o, DateTime t)
    {
        if (o.RadiusKm <= 0 || o.Behavior != SimBehavior.Driving) return (o.Latitude, o.Longitude, 0);
        var loop = Loop(o);
        var lengths = new double[loop.Length];
        var perimeter = 0.0;
        for (var i = 0; i < loop.Length; i++)
        {
            var (a, b) = (loop[i], loop[(i + 1) % loop.Length]);
            lengths[i] = DistanceKm(a.Lat, a.Lon, b.Lat, b.Lon);
            perimeter += lengths[i];
        }
        var d = Km(o, t) % perimeter;
        for (var i = 0; i < loop.Length; i++)
        {
            if (d > lengths[i])
            {
                d -= lengths[i];
                continue;
            }
            var (a, b) = (loop[i], loop[(i + 1) % loop.Length]);
            var f = lengths[i] <= 0 ? 0 : d / lengths[i];
            return (a.Lat + (b.Lat - a.Lat) * f, a.Lon + (b.Lon - a.Lon) * f, Bearing(a.Lat, a.Lon, b.Lat, b.Lon));
        }
        return (loop[0].Lat, loop[0].Lon, 0);
    }

    /// <summary>Prędkość chwilowa [km/h] z niewielkim zróżnicowaniem.</summary>
    public static double Speed(SimObject o, DateTime t) =>
        IsDriving(o, t) ? Math.Round(o.SpeedKmh * (0.75 + 0.5 * Noise(o.Id * 7919 + (int)(t - Epoch).TotalMinutes)), 1) : 0;

    public static int Rpm(SimObject o, DateTime t) =>
        IsEngineOn(o, t) ? (int)Math.Round(IsDriving(o, t) ? 1200 + Speed(o, t) * 15 : 750) : 0;

    /// <summary>Zużycie paliwa [l/km] według typu obiektu.</summary>
    public static double FuelRate(SimObject o) => o.Type switch
    {
        2 or 3 => 0.31,
        4 => 0.36,
        1 or 5 => 0.10,
        _ => 0.07,
    };

    /// <summary>Ostatnie tankowanie przed chwilą <paramref name="t"/>.</summary>
    public static DateTime LastRefuel(DateTime t) => t.TimeOfDay >= RefuelHour ? t.Date + RefuelHour : t.Date.AddDays(-1) + RefuelHour;

    /// <summary>Poziom paliwa [l]; <c>null</c> — obiekt bez zbiornika.</summary>
    public static double? FuelLevel(SimObject o, DateTime t)
    {
        if (o.TankCapacity is not { } cap) return null;
        var level = 0.9 * cap - Km(o, LastRefuel(t), t) * FuelRate(o);
        return Math.Round(Math.Max(0.08 * cap, level), 1);
    }

    /// <summary>Wartość pseudolosowa 0–1 zależna wyłącznie od argumentu.</summary>
    public static double Noise(int seed)
    {
        unchecked
        {
            var x = (uint)seed * 2654435761u;
            x ^= x >> 13;
            x *= 1274126177u;
            x ^= x >> 16;
            return (x & 0xFFFFFF) / (double)0x1000000;
        }
    }

    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 6371 * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    static double Bearing(double lat1, double lon1, double lat2, double lon2)
    {
        var φ1 = lat1 * Math.PI / 180;
        var φ2 = lat2 * Math.PI / 180;
        var Δλ = (lon2 - lon1) * Math.PI / 180;
        var y = Math.Sin(Δλ) * Math.Cos(φ2);
        var x = Math.Cos(φ1) * Math.Sin(φ2) - Math.Sin(φ1) * Math.Cos(φ2) * Math.Cos(Δλ);
        return (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
    }
}
