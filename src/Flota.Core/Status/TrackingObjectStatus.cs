namespace Flota.Core.Status;

/// <summary>Status obiektu; kolejność wartości wyznacza domyślne sortowanie listy obiektów.</summary>
public enum TrackingObjectStatus
{
    Move = 1,
    Stop = 2,
    InvalidGps = 3,
    Outdated = 4,
    Unknown = 5,
}

/// <summary>Wyznaczanie statusu obiektu (odpowiednik <c>TrackingObject.GetTrackingObjectStatus</c>).</summary>
public static class TrackingObjectStatusResolver
{
    /// <summary>Wiek ostatniej wiadomości, po którym dane uznaje się za nieaktualne.</summary>
    public static readonly TimeSpan OutdatedAfter = TimeSpan.FromMinutes(60);

    /// <param name="enabled">Obiekt aktywny (urządzenie włączone).</param>
    /// <param name="lastMessageTime">Czas ostatniej wiadomości; <c>null</c> — brak wiadomości.</param>
    /// <param name="validGps">Poprawna pozycja GPS w ostatniej wiadomości.</param>
    /// <param name="isEngineStarted">Stan stacyjki; <c>null</c> — brak odczytu.</param>
    /// <param name="now">Czas bieżący.</param>
    public static TrackingObjectStatus Resolve(bool enabled, DateTime? lastMessageTime, bool validGps, bool? isEngineStarted, DateTime now)
    {
        if (!enabled || lastMessageTime is null || lastMessageTime == DateTime.MinValue)
            return TrackingObjectStatus.Unknown;
        if (lastMessageTime < now - OutdatedAfter)
            return TrackingObjectStatus.Outdated;
        if (!validGps)
            return TrackingObjectStatus.InvalidGps;
        return isEngineStarted == false ? TrackingObjectStatus.Stop : TrackingObjectStatus.Move;
    }

    /// <summary>Opis statusu wyświetlany w interfejsie.</summary>
    public static string DisplayName(TrackingObjectStatus status) => status switch
    {
        TrackingObjectStatus.Move => "W ruchu",
        TrackingObjectStatus.Stop => "Zatrzymany",
        TrackingObjectStatus.InvalidGps => "Brak GPS",
        TrackingObjectStatus.Outdated => "Nieaktualny",
        _ => "Nieznany",
    };
}
