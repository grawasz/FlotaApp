using Flota.Core.Geo;
using Flota.Core.Status;

namespace Flota.Core.Contracts;

/// <summary>Stan floty: obiekty z ostatnią pozycją, obszary i użytkownik (<c>GET /fleet/snapshot</c>).</summary>
public sealed record FleetSnapshot(
    DateTimeOffset ServerTime,
    UserInfo User,
    IReadOnlyList<FleetObject> Objects,
    IReadOnlyList<AreaInfo> Areas);

/// <summary>Obiekt floty (pojazd, osoba, urządzenie).</summary>
public sealed record FleetObject
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string RegistrationNumber { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string MakeModel { get; init; } = string.Empty;
    /// <summary>Typ obiektu UniAVL (0–22) — wyznacza ikonę.</summary>
    public int Type { get; init; }
    public bool IsActive { get; init; }
    public string? SenderId { get; init; }
    public string DeviceModel { get; init; } = string.Empty;
    /// <summary>Klasyfikacja urządzenia: „GPS CAN” / „GPS standard”.</summary>
    public string DeviceName { get; init; } = string.Empty;
    public string DriverName { get; init; } = string.Empty;
    /// <summary>Pojemność zbiornika [l]; <c>null</c> — brak zbiornika.</summary>
    public int? TankCapacity { get; init; }
    public ObjectPosition? Position { get; init; }

    /// <summary>Obiekt ma pozycję (odpowiednik <c>TrackingObject.Enabled</c>).</summary>
    public bool Enabled => Position is not null;

    public TrackingObjectStatus StatusAt(DateTime now) =>
        TrackingObjectStatusResolver.Resolve(Enabled, Position?.Time, Position?.ValidGps ?? false, Position?.IsEngineStarted, now);
}

/// <summary>Pozycja i odczyty obiektu (ostatnia wiadomość lub punkt trasy).</summary>
public sealed record ObjectPosition
{
    public required DateTime Time { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public bool ValidGps { get; init; }
    public int? Satellites { get; init; }
    public double? Heading { get; init; }
    public double? Speed { get; init; }
    public double? SpeedMax { get; init; }
    public double? SpeedFromCan { get; init; }
    public int? Rpm { get; init; }
    public double? TotalDistanceCan { get; init; }
    /// <summary>Poziom paliwa jako ułamek pojemności zbiornika (0–1).</summary>
    public double? Fuel { get; init; }
    /// <summary>Poziom paliwa [l].</summary>
    public double? FuelLevel { get; init; }
    public bool? IsEngineStarted { get; init; }
    public string? DriverName { get; init; }
    public IReadOnlyList<SensorReading> Sensors { get; init; } = [];
    public AddressInfo? Address { get; init; }

    public bool HasLocation => Latitude != 0 || Longitude != 0;
}

/// <summary>Odczyt czujnika.</summary>
public sealed record SensorReading(
    string Name,
    int Order,
    bool Visible,
    bool? BoolValue,
    string? Value);

/// <summary>Obszar (strefa) na mapie.</summary>
public sealed record AreaInfo(
    int Id,
    string Name,
    bool IsActive,
    /// <summary>Kolor wypełnienia <c>#RRGGBB</c>.</summary>
    string FillColor,
    /// <summary>Kolor obrysu <c>#RRGGBB</c>.</summary>
    string OutlineColor,
    int OutlineThickness,
    IReadOnlyList<GeoPoint> Outline);

public sealed record GeoPoint(double Latitude, double Longitude);

/// <summary>Zmiana pozycji obiektu przekazywana przez hub SignalR.</summary>
public sealed record PositionChanged(int ObjectId, ObjectPosition Position);

/// <summary>Adres ustalony po wysłaniu pozycji.</summary>
public sealed record AddressResolved(int ObjectId, DateTime Time, AddressInfo Address);

/// <summary>Ustawienia użytkownika (widok początkowy mapy).</summary>
public sealed record UserSettings(double? DefaultLatitude, double? DefaultLongitude, double? DefaultZoom);
