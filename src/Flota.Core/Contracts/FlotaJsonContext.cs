using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flota.Core.Contracts;

/// <summary>Serializacja kontraktów Flota.Api (generator źródłowy, bez refleksji).</summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(RefreshRequest))]
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(AppDownloadInfo))]
[JsonSerializable(typeof(FleetSnapshot))]
[JsonSerializable(typeof(PositionChanged))]
[JsonSerializable(typeof(IReadOnlyList<ObjectPosition>))]
[JsonSerializable(typeof(IReadOnlyList<PositionChanged>))]
[JsonSerializable(typeof(AddressResolved))]
[JsonSerializable(typeof(UserSettings))]
[JsonSerializable(typeof(RouteReport))]
[JsonSerializable(typeof(WaypointsRequest))]
[JsonSerializable(typeof(WaypointsResponse))]
[JsonSerializable(typeof(FuelReport))]
[JsonSerializable(typeof(IReadOnlyList<ObjectEvent>))]
[JsonSerializable(typeof(DrivingStyle))]
[JsonSerializable(typeof(IReadOnlyList<RouteStage>))]
[JsonSerializable(typeof(FleetReportRequest))]
[JsonSerializable(typeof(FleetReportResponse))]
[JsonSerializable(typeof(EcoDrivingRequest))]
[JsonSerializable(typeof(IReadOnlyList<EcoDrivingRow>))]
[JsonSerializable(typeof(IReadOnlyList<LogbookInfo>))]
[JsonSerializable(typeof(IReadOnlyList<LogbookEntry>))]
[JsonSerializable(typeof(IReadOnlyList<DriverInfo>))]
[JsonSerializable(typeof(IReadOnlyList<EquipmentInfo>))]
[JsonSerializable(typeof(EventTemplatesRequest))]
[JsonSerializable(typeof(IReadOnlyList<EventTemplateInfo>))]
[JsonSerializable(typeof(IReadOnlyList<AreaInfo>))]
[JsonSerializable(typeof(ExportTable))]
public sealed partial class FlotaJsonContext : JsonSerializerContext;
