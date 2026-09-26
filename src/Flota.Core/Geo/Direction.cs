namespace Flota.Core.Geo;

/// <summary>Kierunek ruchu obiektu (azymut: 0° — północ, rośnie zgodnie z ruchem wskazówek zegara).</summary>
public static class Direction
{
    /// <summary>
    /// Azymut z kursu UniAVL. Kurs jest podawany w zakresie ±180° i mierzony przeciwnie do ruchu wskazówek zegara
    /// (weryfikacja na danych: kierunek ruchu = −kurs; kurs zaokrąglany do 45°).
    /// </summary>
    public static double FromUniAvlHeading(double heading) => Normalize(-heading);

    /// <summary>Azymut odcinka między dwoma punktami (wzór na kurs początkowy po ortodromie).</summary>
    public static double Bearing(double lat1, double lon1, double lat2, double lon2)
    {
        var φ1 = lat1 * Math.PI / 180;
        var φ2 = lat2 * Math.PI / 180;
        var Δλ = (lon2 - lon1) * Math.PI / 180;
        var y = Math.Sin(Δλ) * Math.Cos(φ2);
        var x = Math.Cos(φ1) * Math.Sin(φ2) - Math.Sin(φ1) * Math.Cos(φ2) * Math.Cos(Δλ);
        return Normalize(Math.Atan2(y, x) * 180 / Math.PI);
    }

    /// <summary>
    /// Kierunek strzałki punktu śladu: faktyczny kierunek do następnego punktu (dokładność 1°),
    /// a przy jego braku lub braku przemieszczenia — kierunek z kursu UniAVL.
    /// </summary>
    public static double? ArrowAzimuth(double lat, double lon, double? heading, double? nextLat, double? nextLon)
    {
        if (nextLat is { } nl && nextLon is { } nn && (nl != lat || nn != lon))
            return Bearing(lat, lon, nl, nn);
        return heading is { } h ? FromUniAvlHeading(h) : null;
    }

    static double Normalize(double deg) => ((deg % 360) + 360) % 360;
}
