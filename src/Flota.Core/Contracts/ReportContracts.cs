using Flota.Core.Geo;

namespace Flota.Core.Contracts;

/// <summary>Żądanie raportu dla wielu obiektów.</summary>
public sealed record FleetReportRequest(IReadOnlyList<int> ObjectIds, DateTime From, DateTime To);

/// <summary>Raport floty: podsumowanie na obiekt i szczegóły dzienne.</summary>
public sealed record FleetReportResponse(
    IReadOnlyList<FleetReportRow> Summary,
    IReadOnlyList<FleetReportRow> Details);

/// <summary>Wiersz raportu floty (podsumowanie obiektu lub dzień).</summary>
public sealed record FleetReportRow
{
    public DateTime? Date { get; init; }
    public int ObjectId { get; init; }
    public string ObjectName { get; init; } = string.Empty;
    public string MakeModel { get; init; } = string.Empty;
    public double? Odometer { get; init; }
    public string? RideTime { get; init; }
    public string? StopTime { get; init; }
    public double? Distance { get; init; }
    public double? AverageSpeed { get; init; }
    public double? MaxSpeed { get; init; }
    public int? MaxRpm { get; init; }
    public double? FuelUsed { get; init; }
    public double? FuelConsumption { get; init; }
    public double? FuelConsumptionInTime { get; init; }
    public string? NeutralGear { get; init; }
    public string? NeutralGearMoving { get; init; }
    public string? Doors { get; init; }
    public string? Brake { get; init; }
}

/// <summary>Żądanie rankingu Eco-Driving.</summary>
public sealed record EcoDrivingRequest(IReadOnlyList<int> ObjectIds, DateTime From, DateTime To);

/// <summary>Pozycja rankingu Eco-Driving.</summary>
public sealed record EcoDrivingRow
{
    public int Position { get; init; }
    public int ObjectId { get; init; }
    public string ObjectName { get; init; } = string.Empty;
    public string RegistrationNumber { get; init; } = string.Empty;
    /// <summary>Ocena ogólna 0–100 (średnia cech stylu jazdy).</summary>
    public double? Score { get; init; }
    public double? Speeding { get; init; }
    public double? ExceedRpm { get; init; }
    public double? SuddenBraking { get; init; }
    public double? QuickAcceleration { get; init; }
    public TimeSpan SpeedingTime { get; init; }
    public TimeSpan ExceedRpmTime { get; init; }
    public int? SuddenBrakings { get; init; }
    public int? QuickAccelerations { get; init; }
}

/// <summary>Karta drogowa.</summary>
public sealed record LogbookInfo
{
    public int Id { get; init; }
    public DateTime Date { get; init; }
    public string RegistrationNumber { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public int? BeginOdometer { get; init; }
    public int? EndOdometer { get; init; }
    public int? Distance { get; init; }
    public string DriverName { get; init; } = string.Empty;
}

/// <summary>Pozycja karty drogowej.</summary>
public sealed record LogbookEntry
{
    public int Id { get; init; }
    public string AimTrip { get; init; } = string.Empty;
    public DateTime BeginTime { get; init; }
    public string BeginAddress { get; init; } = string.Empty;
    public GeoPoint? BeginLocation { get; init; }
    public int BeginOdometer { get; init; }
    public DateTime EndTime { get; init; }
    public string EndAddress { get; init; } = string.Empty;
    public GeoPoint? EndLocation { get; init; }
    public int EndOdometer { get; init; }
    public int Distance { get; init; }
    public string DriverName { get; init; } = string.Empty;
}

/// <summary>Kierowca.</summary>
public sealed record DriverInfo(int UserId, string FirstName, string LastName, string Login, bool IsActive);

/// <summary>Wyposażenie obiektu.</summary>
public sealed record EquipmentInfo
{
    public int Id { get; init; }
    public int ObjectId { get; init; }
    public string RegistrationNumber { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public int GpsDeviceType { get; init; }
    public int FuelProbes { get; init; }
    public int AlcoDetectors { get; init; }
    public int VideoRecorders { get; init; }
    public int Cameras { get; init; }
    public int Monitors { get; init; }
}

/// <summary>Szablon zdarzenia lub przypomnienie (termin).</summary>
public sealed record EventTemplateInfo
{
    public int Id { get; init; }
    public int ObjectId { get; init; }
    public string ObjectName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int EventType { get; init; }
    public bool IsReminder { get; init; }
    public string? Duration { get; init; }
    public string? BeginHour { get; init; }
    public string? EndHour { get; init; }
    public DateTime? DateTimeValue { get; init; }
    public string? AreaName { get; init; }
}

public sealed record EventTemplatesRequest(IReadOnlyList<int> ObjectIds);

/// <summary>Tabela do eksportu XLSX (kolumny i wiersze w postaci prezentowanej w aplikacji).</summary>
public sealed record ExportTable(
    string SheetName,
    IReadOnlyList<ExportColumn> Columns,
    IReadOnlyList<IReadOnlyList<string?>> Rows);

public enum ExportColumnKind
{
    Text,
    Number,
    WholeNumber,
    DateTime,
    Date,
    Boolean,
}

public sealed record ExportColumn(string Header, ExportColumnKind Kind = ExportColumnKind.Text);
