using Flota.Core.Geo;

namespace Flota.Core.Contracts;

/// <summary>Parametry raportu trasy (kryteria postoju i stylu jazdy jak w aplikacji Blazor).</summary>
public sealed record RouteQuery
{
    public required DateTime From { get; init; }
    public required DateTime To { get; init; }
    /// <summary>Minimalny czas postoju; <c>null</c> — kryterium wyłączone.</summary>
    public TimeSpan? StopTime { get; init; }
    /// <summary>Minimalny czas wyłączenia stacyjki; <c>null</c> — kryterium wyłączone.</summary>
    public TimeSpan? IgnitionOffTime { get; init; } = TimeSpan.FromMinutes(1);
    /// <summary>Prędkość minimalna [km/h] (tylko z kryterium postoju).</summary>
    public decimal MinSpeed { get; init; }
    public bool StageSubtypeBySensor { get; init; } = true;
}

/// <summary>Raport trasy obiektu.</summary>
public sealed record RouteReport
{
    public required int ObjectId { get; init; }
    public string ObjectName { get; init; } = string.Empty;
    public string RegistrationNumber { get; init; } = string.Empty;
    public int? TankCapacity { get; init; }
    public DateTime? BeginDate { get; init; }
    public DateTime? EndDate { get; init; }
    public TimeSpan RideTime { get; init; }
    public TimeSpan? StopTime { get; init; }
    public TimeSpan? HaulTime { get; init; }
    public double? Distance { get; init; }
    public double? DistanceGps { get; init; }
    public int? MaxRpm { get; init; }
    public double? MaxSpeed { get; init; }
    public double? AverageSpeed { get; init; }
    public double? TotalDistanceCan { get; init; }
    public double? FuelUsed { get; init; }
    /// <summary>Spalanie [l/100 km].</summary>
    public double? FuelConsumption { get; init; }
    /// <summary>Spalanie [l/godz.].</summary>
    public double? FuelInTimeConsumption { get; init; }
    public bool HasFuelData { get; init; }
    /// <summary>Aktywność i wartości czujników w okresie (nazwa czujnika → wartość).</summary>
    public IReadOnlyList<NamedValue> SensorActivity { get; init; } = [];
    public IReadOnlyList<RouteStage> Stages { get; init; } = [];
    /// <summary>Kolumny dynamiczne kursów (klucz → nagłówek).</summary>
    public IReadOnlyList<NamedValue> StageColumns { get; init; } = [];
}

public sealed record NamedValue(string Name, string Value);

public enum StageType
{
    Ride = 0,
    Stop = 1,
    Haul = 2,
}

/// <summary>Kurs (etap trasy).</summary>
public sealed record RouteStage
{
    public required int Index { get; init; }
    public required StageType Type { get; init; }
    public required DateTime BeginDate { get; init; }
    public required DateTime EndDate { get; init; }
    /// <summary>Czas trwania w postaci tekstowej z UniAVL (np. „01:02:03”).</summary>
    public string Time { get; init; } = string.Empty;
    public double? Distance { get; init; }
    public double? AverageSpeed { get; init; }
    public double? MaxSpeed { get; init; }
    public int? MaxRpm { get; init; }
    public double? FuelUsed { get; init; }
    public double? FuelUsage { get; init; }
    public double? TotalDistanceCan { get; init; }
    public string? DriverName { get; init; }
    /// <summary>Wartości kolumn dynamicznych (klucz z <see cref="RouteReport.StageColumns"/> → wartość).</summary>
    public IReadOnlyList<NamedValue> Values { get; init; } = [];
    /// <summary>Adres (podsumowanie kursów — tylko postoje).</summary>
    public AddressInfo? Address { get; init; }

    public string TypeName => Type switch
    {
        StageType.Ride => "Ruch",
        StageType.Stop => "Postój",
        StageType.Haul => "Holowanie",
        _ => string.Empty,
    };
}

/// <summary>Okno czasowe kursu, dla którego pobierane są punkty trasy.</summary>
public sealed record StageWindow(StageType Type, DateTime BeginDate, DateTime EndDate);

public sealed record WaypointsRequest(IReadOnlyList<StageWindow> Stages);

/// <summary>Punkty trasy z kolumnami czujników.</summary>
public sealed record WaypointsResponse(
    IReadOnlyList<string> SensorColumns,
    IReadOnlyList<Waypoint> Points);

/// <summary>Punkt trasy.</summary>
public sealed record Waypoint
{
    public required int Id { get; init; }
    public required ObjectPosition Position { get; init; }
    /// <summary>Wartości czujników w kolejności <see cref="WaypointsResponse.SensorColumns"/>.</summary>
    public IReadOnlyList<SensorReading?> SensorValues { get; init; } = [];
}

/// <summary>Raport paliwa.</summary>
public sealed record FuelReport
{
    public double? FuelUsed { get; init; }
    public double? StartLevel { get; init; }
    public double? FinalLevel { get; init; }
    public double Fueled { get; init; }
    public IReadOnlyList<FuelEvent> Refueling { get; init; } = [];
    public IReadOnlyList<FuelEvent> Defueling { get; init; } = [];
    public IReadOnlyList<TimeValue> TimeSeries { get; init; } = [];
    public IReadOnlyList<DistanceValue> DistanceSeries { get; init; } = [];
}

public sealed record FuelEvent(DateTime Date, double Fuel, AddressInfo? Address);

public sealed record TimeValue(DateTime Time, double Value);

public sealed record DistanceValue(double Kilometers, double Value, DateTime Time);

/// <summary>Zdarzenie obiektu.</summary>
public sealed record ObjectEvent(
    DateTime Date,
    DateTime? EndDate,
    int EventType,
    string Description,
    double Fuel,
    double? Latitude,
    double? Longitude,
    AddressInfo? Address);

/// <summary>Parametry stylu jazdy.</summary>
public sealed record DrivingStyleQuery
{
    public required RouteQuery Route { get; init; }
    public decimal Speeding { get; init; } = 50;
    public decimal ExceedRpm { get; init; } = 2000;
    public decimal SuddenBraking { get; init; } = 1;
    public decimal QuickAcceleration { get; init; } = 1;
}

/// <summary>Styl jazdy.</summary>
public sealed record DrivingStyle(
    TimeSpan SpeedingTime,
    TimeSpan ExceedRpmTime,
    int? SuddenBrakings,
    int? QuickAccelerations,
    IReadOnlyList<DrivingStyleFeature> Features);

/// <summary>Cecha stylu jazdy (wartość 0–100).</summary>
public sealed record DrivingStyleFeature(int Type, string Name, double? Value);
