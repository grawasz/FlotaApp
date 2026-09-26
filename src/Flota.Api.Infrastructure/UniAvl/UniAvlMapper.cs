using System.Globalization;
using System.Text.Json;
using Flota.Api.Infrastructure.UniAvl.Dto;
using Flota.Core.Contracts;
using Flota.Core.Geo;
using Flota.Core.Objects;
using Flota.Core.Reports;

namespace Flota.Api.Infrastructure.UniAvl;

/// <summary>Przekształcenie kontraktu UniAVL w kontrakt Flota.Api.</summary>
public static class UniAvlMapper
{
    static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");

    public static UserInfo ToUser(AvlIdentity identity, string login) => new(
        identity.UserId,
        identity.Login ?? login,
        identity.FirstName ?? string.Empty,
        identity.LastName ?? string.Empty,
        identity.Company?.CompanyName);

    public static FleetObject ToFleetObject(AvlTrackingObject o, ObjectPosition? position) => new()
    {
        Id = o.TrackingObjectId,
        Name = o.TrackingObjectName ?? string.Empty,
        RegistrationNumber = o.RegistrationNumber ?? string.Empty,
        Manufacturer = o.Manufacturer ?? string.Empty,
        Model = o.Model ?? string.Empty,
        MakeModel = o.MakeModel ?? string.Empty,
        Type = o.TrackingObjectType,
        IsActive = o.IsActive,
        SenderId = o.Device?.SenderId,
        DeviceModel = o.Device?.Model ?? string.Empty,
        DeviceName = DeviceClassifier.Classify(o.Device?.Model, o.Device is not null),
        DriverName = o.Driver is null ? string.Empty : $"{o.Driver.FirstName} {o.Driver.LastName}".Trim(),
        TankCapacity = o.Tank?.Capacity,
        Position = position,
    };

    /// <summary>
    /// Ostatnie wiadomości przypisane do obiektów: po <c>SenderId</c> urządzenia, a przy jego braku — po wspólnym czujniku
    /// (odpowiednik <c>TrackingObject.RefreshMessages</c>, dopasowanie w czasie stałym — P8).
    /// </summary>
    public static Dictionary<int, AvlTrackingMessage> MatchMessages(IReadOnlyList<AvlTrackingObject> objects, IReadOnlyList<AvlTrackingMessage> messages)
    {
        var bySender = new Dictionary<string, AvlTrackingMessage>(StringComparer.Ordinal);
        var bySensor = new Dictionary<int, AvlTrackingMessage>();
        foreach (var m in messages)
        {
            if (!string.IsNullOrEmpty(m.SenderId)
                && (!bySender.TryGetValue(m.SenderId, out var existing) || m.Timedate > existing.Timedate))
                bySender[m.SenderId] = m;
            foreach (var s in m.Sensors ?? [])
                bySensor.TryAdd(s.SensorId, m);
        }

        var result = new Dictionary<int, AvlTrackingMessage>();
        foreach (var o in objects)
        {
            if (o.Device?.SenderId is { Length: > 0 } sender && bySender.TryGetValue(sender, out var m))
            {
                result[o.TrackingObjectId] = m;
                continue;
            }
            foreach (var s in o.Sensors ?? [])
            {
                if (bySensor.TryGetValue(s.SensorId, out var ms))
                {
                    result[o.TrackingObjectId] = ms;
                    break;
                }
            }
        }
        return result;
    }

    public static ObjectPosition ToPosition(AvlTrackingMessage m, int? tankCapacity, AddressInfo? address = null) => new()
    {
        Time = m.Timedate,
        Latitude = m.Location?.Latitude ?? 0,
        Longitude = m.Location?.Longitude ?? 0,
        ValidGps = m.ValidGPS == true,
        Satellites = m.Satellites,
        Heading = m.Heading,
        Speed = m.Speed,
        SpeedMax = m.SpeedMax,
        SpeedFromCan = m.SpeedFromCAN,
        Rpm = m.RPM,
        TotalDistanceCan = m.TotalDistanceCAN,
        Fuel = m.Fuel,
        FuelLevel = ReportRules.FuelLevel(m.Fuel, tankCapacity),
        IsEngineStarted = m.IsEngineStarted,
        DriverName = m.DriverName,
        Sensors = (m.Sensors ?? []).Select(ToSensor).ToList(),
        Address = address,
    };

    public static SensorReading ToSensor(AvlSensor s)
    {
        bool? b = s.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
        return new SensorReading((s.SensorName ?? string.Empty).Trim(), s.Order, s.Visible == true, b, b is null ? FormatJson(s.Value) : null);
    }

    public static AreaInfo ToArea(AvlArea a) => new(
        a.AreaId,
        a.AreaName ?? string.Empty,
        a.IsActive,
        GeoConversions.Win32ToHex(a.FillColor),
        GeoConversions.Win32ToHex(a.OutlineColor),
        a.OutlineThickness,
        GeoConversions.ParseWktPolygon(a.Outline));

    public static RouteReport ToRouteReport(int objectId, AvlRoute r, FleetObject? fleetObject)
    {
        var stages = r.Stages ?? [];
        var columns = StageColumns(stages);
        var consumption = r.FuelConsumption ?? ReportRules.ConsumptionPer100Km(r.FuelUsed, r.Distance);
        var perHour = r.FuelInTimeConsumption ?? ReportRules.ConsumptionPerHour(r.FuelUsed, r.RideTime);
        return new RouteReport
        {
            ObjectId = objectId,
            ObjectName = r.TrackingObject?.TrackingObjectName ?? fleetObject?.Name ?? string.Empty,
            RegistrationNumber = r.TrackingObject?.RegistrationNumber ?? fleetObject?.RegistrationNumber ?? string.Empty,
            TankCapacity = r.TrackingObject?.Tank?.Capacity ?? fleetObject?.TankCapacity,
            BeginDate = r.BeginDate,
            EndDate = r.EndDate,
            RideTime = r.RideTime,
            StopTime = r.StopTime,
            HaulTime = r.HaulTime,
            Distance = r.Distance,
            DistanceGps = r.DistanceGPS,
            MaxRpm = r.MaxRPM,
            MaxSpeed = r.MaxSpeed,
            AverageSpeed = r.AverageSpeed,
            TotalDistanceCan = r.TotalDistanceCAN,
            FuelUsed = r.FuelUsed,
            FuelConsumption = consumption,
            FuelInTimeConsumption = perHour,
            HasFuelData = r.FuelUsed.HasValue || consumption.HasValue || perHour.HasValue || r.TrackingObject?.Tank is not null,
            SensorActivity = SensorActivity(r.SensorsActivityTime, r.SensorsValue),
            StageColumns = columns,
            Stages = stages.Select((s, i) => ToStage(i, s, columns)).ToList(),
        };
    }

    /// <summary>
    /// Kolumny dynamiczne kursów (odpowiednik <c>stageCols</c>): na podstawie dwóch pierwszych kursów —
    /// stan licznika, zużycie i spalanie paliwa oraz czasy aktywności czujników.
    /// </summary>
    static List<NamedValue> StageColumns(IReadOnlyList<AvlStage> stages)
    {
        var cols = new List<NamedValue>();
        void Add(string key, string header)
        {
            if (cols.All(c => c.Name != key)) cols.Add(new NamedValue(key, header));
        }
        foreach (var s in stages.Take(2))
        {
            if (s.TotalDistanceCAN is not null) Add("TotalDistanceCAN", "Stan licznika [km]");
            if (s.FuelUsed is not null) Add("FuelUsed", "Zużycie paliwa [l]");
            if (s.FuelUsed is not null && s.Distance is not null) Add("FuelUsage", "Spalanie [l/100km]");
        }
        foreach (var s in stages.Take(2))
            foreach (var (name, value) in s.SensorsActivityTime ?? [])
                if (!IsMetadata(name) && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
                    Add("S:" + name, SensorLabel(name));
        return cols;
    }

    static RouteStage ToStage(int index, AvlStage s, IReadOnlyList<NamedValue> columns)
    {
        var usage = ReportRules.ConsumptionPer100Km(s.FuelUsed, s.Distance);
        var values = new List<NamedValue>(columns.Count);
        foreach (var c in columns)
        {
            var v = c.Name switch
            {
                "TotalDistanceCAN" => s.TotalDistanceCAN?.ToString("N0", Pl),
                "FuelUsed" => s.FuelUsed?.ToString("N1", Pl),
                "FuelUsage" => usage?.ToString("N1", Pl),
                _ when s.SensorsActivityTime?.TryGetValue(c.Name[2..], out var e) == true => FormatJson(e),
                _ => null,
            };
            values.Add(new NamedValue(c.Name, v ?? string.Empty));
        }
        return new RouteStage
        {
            Index = index,
            Type = (StageType)s.StageType,
            BeginDate = s.BeginDate,
            EndDate = s.EndDate,
            Time = s.Time ?? string.Empty,
            Distance = s.Distance,
            AverageSpeed = s.AverageSpeed,
            MaxSpeed = s.MaxSpeed,
            MaxRpm = s.MaxRPM,
            FuelUsed = s.FuelUsed,
            FuelUsage = usage,
            TotalDistanceCan = s.TotalDistanceCAN,
            DriverName = s.DriverName,
            Values = values,
        };
    }

    /// <summary>
    /// Aktywność czujników w okresie (odpowiednik panelu „Aktywność czujników”): czasy aktywności bez stacyjki
    /// i wartości zerowych, następnie wartości czujników zaokrąglone do 0,1.
    /// </summary>
    static List<NamedValue> SensorActivity(Dictionary<string, JsonElement>? activity, Dictionary<string, JsonElement>? values)
    {
        var list = new List<NamedValue>();
        foreach (var (name, v) in activity ?? [])
        {
            if (IsMetadata(name) || name.Contains("stacyjka", StringComparison.OrdinalIgnoreCase)) continue;
            var text = FormatJson(v);
            if (string.IsNullOrEmpty(text) || text is "0" or "00:00:00") continue;
            list.Add(new NamedValue(SensorLabel(name), text));
        }
        foreach (var (name, v) in values ?? [])
        {
            if (IsMetadata(name)) continue;
            var label = SensorLabel(name);
            if (list.Any(x => x.Name == label)) continue;
            var text = v.ValueKind == JsonValueKind.Number ? Math.Round(v.GetDouble(), 1).ToString("0.#", Pl) : FormatJson(v);
            if (!string.IsNullOrEmpty(text)) list.Add(new NamedValue(label, text));
        }
        return list;
    }

    public static FuelReport ToFuelReport(AvlFuelReport f, IReadOnlyDictionary<(int, int), AddressInfo> addresses)
    {
        var events = f.Events ?? [];
        FuelEvent Map(AvlEvent e) => new(e.EventDate, e.Fuel, AddressFor(e.Location, addresses));
        var refuel = events.Where(e => e.Fuel > 0).Select(Map).ToList();
        var defuel = events.Where(e => e.Fuel < 0).Select(Map).ToList();
        var time = (f.TimeChartData ?? []).Select(t => new TimeValue(t.Argument, t.Value)).ToList();
        var distance = (f.DistanceChartData?.Values ?? []).Select(d => new DistanceValue(d.Argument, d.Value, d.Hint)).ToList();
        return new FuelReport
        {
            FuelUsed = f.FuelUsed,
            StartLevel = time.Count > 0 ? time[0].Value : null,
            FinalLevel = time.Count > 0 ? time[^1].Value : null,
            Fueled = refuel.Sum(e => e.Fuel),
            Refueling = refuel,
            Defueling = defuel,
            TimeSeries = time,
            DistanceSeries = distance,
        };
    }

    public static ObjectEvent ToEvent(AvlEvent e, IReadOnlyDictionary<(int, int), AddressInfo> addresses) => new(
        e.EventDate,
        e.EventEndDate,
        e.EventType,
        e.Description ?? string.Empty,
        e.Fuel,
        e.Location?.Latitude,
        e.Location?.Longitude,
        AddressFor(e.Location, addresses));

    public static DrivingStyle ToDrivingStyle(AvlDrivingStyle d) => new(
        d.SpeedingTime,
        d.ExceedRpmTime,
        d.NumberOfSuddenBrakings,
        d.NumberOfQuickAccelerations,
        (d.DrivingStyleFeatures ?? [])
            .Select(f => new DrivingStyleFeature(f.FeatureType, ReportRules.DrivingFeatureName(f.FeatureType), ReportRules.DrivingFeatureScore(f.Value)))
            .ToList());

    public static FleetReportRow ToFleetRow(int objectId, string objectName, string makeModel, DateTime? date,
        AvlRouteDetail? route, AvlFuelDetail? fuel, IEnumerable<AvlSensorDetail>? sensors)
    {
        var s = sensors?.ToList() ?? [];
        string? Activity(string name) => s.FirstOrDefault(x => string.Equals(x.SensorName?.Trim(), name, StringComparison.Ordinal))?.ActivityTime;
        return new FleetReportRow
        {
            Date = date,
            ObjectId = objectId,
            ObjectName = objectName,
            MakeModel = makeModel,
            Odometer = route?.Odometer,
            RideTime = route?.RideTime,
            StopTime = route?.StopTime,
            Distance = route?.Distance,
            AverageSpeed = route?.AverageSpeed,
            MaxSpeed = route?.MaxSpeed,
            MaxRpm = route?.MaxRPM,
            FuelUsed = fuel?.FuelUsed,
            FuelConsumption = fuel?.FuelConsumption,
            FuelConsumptionInTime = fuel?.FuelConsumptionInTime,
            NeutralGear = Activity("Bieg neutralny"),
            NeutralGearMoving = Activity("Bieg neutralny w ruchu"),
            Doors = Activity("Drzwi"),
            Brake = Activity("Hamulec"),
        };
    }

    public static LogbookInfo ToLogbook(AvlLogbook l) => new()
    {
        Id = l.LogbookId,
        Date = l.Date,
        RegistrationNumber = l.TrackingObject?.RegistrationNumber ?? string.Empty,
        ObjectName = l.TrackingObject?.TrackingObjectName ?? string.Empty,
        BeginOdometer = l.LogbookDetails?.BeginOdometer,
        EndOdometer = l.LogbookDetails?.EndOdometer,
        Distance = l.LogbookDetails?.Distance,
        DriverName = DriverName(l.LogbookDetails?.Driver),
    };

    public static LogbookEntry ToLogbookEntry(AvlLogbookDetails d) => new()
    {
        Id = d.LogbookDetailId,
        AimTrip = d.AimTrip ?? string.Empty,
        BeginTime = d.BeginTime,
        BeginAddress = d.BeginAddress ?? string.Empty,
        BeginLocation = d.BeginLocation is null ? null : new GeoPoint(d.BeginLocation.Latitude, d.BeginLocation.Longitude),
        BeginOdometer = d.BeginOdometer,
        EndTime = d.EndTime,
        EndAddress = d.EndAddress ?? string.Empty,
        EndLocation = d.EndLocation is null ? null : new GeoPoint(d.EndLocation.Latitude, d.EndLocation.Longitude),
        EndOdometer = d.EndOdometer,
        Distance = d.Distance,
        DriverName = DriverName(d.Driver),
    };

    public static DriverInfo ToDriver(AvlDriver d) => new(d.UserId, d.FirstName ?? string.Empty, d.LastName ?? string.Empty, d.Login ?? string.Empty, d.IsActive);

    public static EquipmentInfo ToEquipment(AvlEquipment e) => new()
    {
        Id = e.EquipmentId,
        ObjectId = e.TrackingObject?.TrackingObjectId ?? 0,
        RegistrationNumber = e.TrackingObject?.RegistrationNumber ?? string.Empty,
        ObjectName = e.TrackingObject?.TrackingObjectName ?? string.Empty,
        DeviceName = e.TrackingObject is null ? string.Empty : DeviceClassifier.Classify(e.TrackingObject.Device?.Model, e.TrackingObject.Device is not null),
        GpsDeviceType = e.GpsDeviceType,
        FuelProbes = e.NumberOfFuelProbes,
        AlcoDetectors = e.NumberOfAlcoDetectors,
        VideoRecorders = e.NumberOfVideoRecorders,
        Cameras = e.NumberOfCameras,
        Monitors = e.NumberOfMonitors,
    };

    public static EventTemplateInfo ToEventTemplate(AvlEventTemplate t, int objectId, string objectName) => new()
    {
        Id = t.EventTemplateId,
        ObjectId = objectId,
        ObjectName = objectName,
        Name = t.EventTemplateName ?? string.Empty,
        EventType = t.EventType,
        IsReminder = t.EventType == ReportRules.ReminderEventType,
        // „Czas trwania” — w aplikacji źródłowej pole nie jest wypełniane (znaczenie IntValue nieustalone)
        Duration = null,
        BeginHour = t.BeginHour,
        EndHour = t.EndHour,
        DateTimeValue = t.DateTimeValue,
        AreaName = t.Area?.AreaName,
    };

    public static AddressInfo? AddressFor(AvlLocation? location, IReadOnlyDictionary<(int, int), AddressInfo> addresses) =>
        location is null ? null : addresses.GetValueOrDefault(GeoConversions.AddressKey(location.Latitude, location.Longitude));

    static string DriverName(AvlDriver? d) => d is null ? string.Empty : $"{d.FirstName} {d.LastName}".Trim();

    static bool IsMetadata(string name) => name.StartsWith('$');

    /// <summary>Nazwa czujnika bez końcowych spacji i dwukropków (klucze UniAVL, np. „Temp. zew. [C]: ”).</summary>
    static string SensorLabel(string key) => key.Trim().TrimEnd(':').Trim();

    static string FormatJson(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? string.Empty,
        JsonValueKind.Number => v.TryGetInt64(out var l) ? l.ToString(Pl) : v.GetDouble().ToString("0.###", Pl),
        JsonValueKind.True => "tak",
        JsonValueKind.False => "nie",
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        _ => v.GetRawText(),
    };
}
