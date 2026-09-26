using System.Text.Json;
using System.Text.Json.Serialization;

// Kontrakt UniAVL REST API (serwer .NET z Newtonsoft PreserveReferencesHandling):
// obiekty zawierają "$id", powtórzenia to {"$ref":"N"}, część kolekcji ma postać {"$type":…,"$values":[…]}.
// Pola metadanych nie są mapowane; wszystkie wartości opcjonalne.
#pragma warning disable CA1819, CA2227 // Tablice i kolekcje z setterem — typy danych deserializacji

namespace Flota.Api.Infrastructure.UniAvl.Dto;

public sealed class AvlTrackingObject
{
    public int TrackingObjectId { get; set; }
    public string? TrackingObjectName { get; set; }
    public AvlCompany? Company { get; set; }
    public AvlDevice? Device { get; set; }
    public bool IsActive { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? Manufacturer { get; set; }
    public string? MakeModel { get; set; }
    public string? Model { get; set; }
    public int TrackingObjectType { get; set; }
    public int FuelType { get; set; }
    public List<AvlSensor>? Sensors { get; set; }
    public AvlDriver? Driver { get; set; }
    public AvlTank? Tank { get; set; }
    public bool? HasLocationData { get; set; }
}

public sealed class AvlCompany
{
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
}

public sealed class AvlDevice
{
    public int DeviceId { get; set; }
    public string? SenderId { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public bool IsActive { get; set; }
    public bool CAN { get; set; }
    public int DeviceType { get; set; }
}

public sealed class AvlSensor
{
    public int SensorId { get; set; }
    public string? SensorName { get; set; }
    public int SensorType { get; set; }
    public int Order { get; set; }
    public string? Mask { get; set; }
    public bool? Visible { get; set; }
    public JsonElement Value { get; set; }
}

public sealed class AvlTank
{
    public int TankId { get; set; }
    public string? TankName { get; set; }
    public int Capacity { get; set; }
    public int InputIndex { get; set; }
}

public sealed class AvlDriver
{
    public int UserId { get; set; }
    public string? Login { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; }
}

public sealed class AvlLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? AreaName { get; set; }
}

public sealed class AvlTrackingMessage
{
    public double TrackingMessageId { get; set; }
    public string? SenderId { get; set; }
    public AvlLocation? Location { get; set; }
    public bool? ValidGPS { get; set; }
    public int? Satellites { get; set; }
    public double? Heading { get; set; }
    public double? Speed { get; set; }
    public DateTime Timedate { get; set; }
    public bool? IsEngineStarted { get; set; }
    public double? SpeedMax { get; set; }
    public List<AvlSensor>? Sensors { get; set; }
    public string? DriverName { get; set; }
    public string? DriverId { get; set; }
    public double? TotalDistanceCAN { get; set; }
    public double? SpeedFromCAN { get; set; }
    public int? RPM { get; set; }
    public double? Fuel { get; set; }
}

public sealed class AvlIdentity
{
    public int UserId { get; set; }
    public string? Login { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public AvlCompany? Company { get; set; }
    public int Role { get; set; }
    public bool IsActive { get; set; }
}

public sealed class AvlArea
{
    public int AreaId { get; set; }
    public string? AreaName { get; set; }
    public string? Outline { get; set; }
    public int FillColor { get; set; }
    public int OutlineColor { get; set; }
    public int OutlineThickness { get; set; }
    public bool IsActive { get; set; }
}

public sealed class AvlRoute
{
    public AvlTrackingObject? TrackingObject { get; set; }
    public DateTime? BeginDate { get; set; }
    public DateTime? EndDate { get; set; }
    public TimeSpan RideTime { get; set; }
    public TimeSpan? HaulTime { get; set; }
    public TimeSpan? StopTime { get; set; }
    public double? Distance { get; set; }
    public double? DistanceGPS { get; set; }
    public int? MaxRPM { get; set; }
    public double? MaxSpeed { get; set; }
    public double? AverageSpeed { get; set; }
    public double? FuelUsed { get; set; }
    public double? FuelConsumption { get; set; }
    public double? FuelInTimeConsumption { get; set; }
    public double? TotalDistanceCAN { get; set; }
    public Dictionary<string, JsonElement>? SensorsActivityTime { get; set; }
    public Dictionary<string, JsonElement>? SensorsValue { get; set; }
    public List<AvlStage>? Stages { get; set; }
}

public sealed class AvlStage
{
    public Dictionary<string, JsonElement>? SensorsActivityTime { get; set; }
    public int StageType { get; set; }
    public DateTime BeginDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Time { get; set; }
    public double? Distance { get; set; }
    public int? MaxRPM { get; set; }
    public double? MaxSpeed { get; set; }
    public double? AverageSpeed { get; set; }
    public double? FuelUsed { get; set; }
    public string? DriverName { get; set; }
    public double? TotalDistanceCAN { get; set; }
}

public sealed class AvlFuelReport
{
    public double? FuelUsed { get; set; }
    public List<AvlEvent>? Events { get; set; }
    public List<AvlTimeChartData>? TimeChartData { get; set; }
    public AvlValues<AvlDistanceValue>? DistanceChartData { get; set; }
}

public sealed class AvlTimeChartData
{
    public DateTime Argument { get; set; }
    public double Value { get; set; }
}

public sealed class AvlDistanceValue
{
    public double Argument { get; set; }
    public double Value { get; set; }
    public DateTime Hint { get; set; }
}

/// <summary>Opakowanie kolekcji <c>{"$type":…,"$values":[…]}</c>.</summary>
public sealed class AvlValues<T>
{
    [JsonPropertyName("$values")]
    public List<T>? Values { get; set; }
}

public sealed class AvlEvent
{
    public double Fuel { get; set; }
    public DateTime EventDate { get; set; }
    public DateTime? EventEndDate { get; set; }
    public int EventType { get; set; }
    public string? Description { get; set; }
    public AvlLocation? Location { get; set; }
    public AvlEventTemplate? EventTemplate { get; set; }
}

public sealed class AvlEventTemplate
{
    public int EventTemplateId { get; set; }
    public int EventType { get; set; }
    public bool Entry { get; set; }
    public string? EventTemplateName { get; set; }
    public string? BeginHour { get; set; }
    public string? EndHour { get; set; }
    public int? IntValue { get; set; }
    public DateTime? DateTimeValue { get; set; }
    public AvlArea? Area { get; set; }
}

public sealed class AvlDrivingStyle
{
    public List<AvlDrivingStyleFeature>? DrivingStyleFeatures { get; set; }
    public TimeSpan SpeedingTime { get; set; }
    public TimeSpan ExceedRpmTime { get; set; }
    public int? NumberOfSuddenBrakings { get; set; }
    public int? NumberOfQuickAccelerations { get; set; }
}

public sealed class AvlDrivingStyleFeature
{
    public int FeatureType { get; set; }
    public double? Value { get; set; }
}

public sealed class AvlFleetReport
{
    public List<AvlDailyReport>? DailyReports { get; set; }
    public AvlRouteDetail? DailyReportRouteDetail { get; set; }
    public AvlFuelDetail? DailyReportFuelDetail { get; set; }
    public List<AvlSensorDetail>? DailyReportSensorDetails { get; set; }
}

public sealed class AvlDailyReport
{
    public int? DailyReportId { get; set; }
    public DateTime? DateTime { get; set; }
    public AvlTrackingObject? TrackingObject { get; set; }
    public AvlRouteDetail? DailyReportRouteDetail { get; set; }
    public AvlFuelDetail? DailyReportFuelDetail { get; set; }
    public AvlValues<AvlSensorDetail>? DailyReportSensorDetails { get; set; }
}

public sealed class AvlRouteDetail
{
    public string? RideTime { get; set; }
    public string? StopTime { get; set; }
    public string? HaulTime { get; set; }
    public double? Distance { get; set; }
    public int? MaxRPM { get; set; }
    public double? MaxSpeed { get; set; }
    public double? AverageSpeed { get; set; }
    public int? NumberOfSuddenBrakings { get; set; }
    public int? NumberOfQuickAccelerations { get; set; }
    public double? Odometer { get; set; }
}

public sealed class AvlFuelDetail
{
    public double? FuelUsed { get; set; }
    public double? FuelConsumption { get; set; }
    public double? FuelConsumptionInTime { get; set; }
}

public sealed class AvlSensorDetail
{
    public string? SensorName { get; set; }
    public string? ActivityTime { get; set; }
}

public sealed class AvlLogbook
{
    public int LogbookId { get; set; }
    public DateTime Date { get; set; }
    public AvlTrackingObject? TrackingObject { get; set; }
    public AvlLogbookDetails? LogbookDetails { get; set; }
}

public sealed class AvlLogbookDetails
{
    public int LogbookDetailId { get; set; }
    public string? AimTrip { get; set; }
    public DateTime BeginTime { get; set; }
    public DateTime EndTime { get; set; }
    public AvlLocation? BeginLocation { get; set; }
    public AvlLocation? EndLocation { get; set; }
    public string? BeginAddress { get; set; }
    public string? EndAddress { get; set; }
    public int BeginOdometer { get; set; }
    public int EndOdometer { get; set; }
    public int Distance { get; set; }
    public AvlDriver? Driver { get; set; }
}

public sealed class AvlEquipment
{
    public int EquipmentId { get; set; }
    public AvlTrackingObject? TrackingObject { get; set; }
    public int GpsDeviceType { get; set; }
    public int NumberOfFuelProbes { get; set; }
    public int NumberOfAlcoDetectors { get; set; }
    public int NumberOfVideoRecorders { get; set; }
    public int NumberOfMonitors { get; set; }
    public int NumberOfCameras { get; set; }
}

/// <summary>Serializacja kontraktu UniAVL (generator źródłowy). Nazwy właściwości zgodne z API (PascalCase).</summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = false,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(List<AvlTrackingObject>))]
[JsonSerializable(typeof(List<AvlTrackingMessage>))]
[JsonSerializable(typeof(AvlTrackingMessage))]
[JsonSerializable(typeof(AvlIdentity))]
[JsonSerializable(typeof(List<AvlArea>))]
[JsonSerializable(typeof(AvlRoute))]
[JsonSerializable(typeof(AvlFuelReport))]
[JsonSerializable(typeof(List<AvlEvent>))]
[JsonSerializable(typeof(List<AvlEventTemplate>))]
[JsonSerializable(typeof(AvlDrivingStyle))]
[JsonSerializable(typeof(AvlFleetReport))]
[JsonSerializable(typeof(List<AvlLogbook>))]
[JsonSerializable(typeof(List<AvlLogbookDetails>))]
[JsonSerializable(typeof(List<AvlDriver>))]
[JsonSerializable(typeof(List<AvlEquipment>))]
[JsonSerializable(typeof(string))]
internal sealed partial class UniAvlJsonContext : JsonSerializerContext;
