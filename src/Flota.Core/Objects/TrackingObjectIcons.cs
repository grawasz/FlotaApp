namespace Flota.Core.Objects;

/// <summary>Ikona typu obiektu (odpowiednik <c>TrackingObject.Image</c>).</summary>
public static class TrackingObjectIcons
{
    static readonly string[] ByType =
    [
        "to_car", "to_van", "to_truck", "to_tractor_unit", "to_bus", "to_ambulance", "to_police",
        "to_firetruck", "to_snowplow", "to_hauler", "to_cement_mixer", "to_helicopter", "to_motorbike",
        "to_bike", "to_horseman", "to_guard", "to_man", "to_tractor", "to_road_roller",
        "to_portable_radio", "to_mobile_radio", "to_truck_trailer", "to_excavator",
    ];

    /// <summary>Typ obiektu „radiotelefon przenośny” — bez odczytu stacyjki.</summary>
    public const int PortableRadioType = 19;

    /// <summary>Nazwa pliku ikony (bez rozszerzenia) dla typu obiektu UniAVL.</summary>
    public static string IconKey(int trackingObjectType) =>
        trackingObjectType >= 0 && trackingObjectType < ByType.Length ? ByType[trackingObjectType] : ByType[0];
}
