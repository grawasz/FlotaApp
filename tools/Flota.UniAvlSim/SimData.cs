using System.Globalization;
using System.Text.Json;
using Flota.Api.Infrastructure.UniAvl.Dto;

namespace Flota.UniAvlSim;

/// <summary>Odpowiedzi UniAVL wyznaczane z modelu ruchu <see cref="SimMotion"/> dla bieżącego czasu.</summary>
internal sealed class SimData(TimeProvider time)
{
    const int MaxMessages = 20_000;
    static readonly JsonElement True = JsonDocument.Parse("true").RootElement.Clone();
    static readonly JsonElement False = JsonDocument.Parse("false").RootElement.Clone();
    static readonly JsonElement Null = JsonDocument.Parse("null").RootElement.Clone();

    DateTime Now => time.GetLocalNow().DateTime;

    // ---------------- Słowniki ----------------

    public static AvlIdentity Identity(string login) => new()
    {
        UserId = SimFleet.UserId,
        Login = login,
        FirstName = "Użytkownik",
        LastName = "demonstracyjny",
        Company = Company,
        Role = 1,
        IsActive = true,
    };

    static AvlCompany Company => new() { CompanyId = 1, CompanyName = SimFleet.CompanyName };

    public static List<AvlTrackingObject> TrackingObjects() => SimFleet.Objects.Select(TrackingObject).ToList();

    static AvlTrackingObject TrackingObject(SimObject o) => new()
    {
        TrackingObjectId = o.Id,
        TrackingObjectName = o.Name,
        Company = Company,
        Device = o.SenderId is null ? null : new AvlDevice
        {
            DeviceId = 1000 + o.Id,
            SenderId = o.SenderId,
            Model = o.DeviceModel,
            SerialNumber = string.Create(CultureInfo.InvariantCulture, $"SN{o.Id:D6}"),
            IsActive = true,
            CAN = o.HasCan,
            DeviceType = 1,
        },
        IsActive = o.IsActive,
        RegistrationNumber = o.Registration,
        Manufacturer = o.Manufacturer,
        Model = o.Model,
        MakeModel = $"{o.Manufacturer} {o.Model}".Trim(),
        TrackingObjectType = o.Type,
        Sensors = SensorDefinitions(o).Select(s => Sensor(s, Null)).ToList(),
        Driver = Driver(SimFleet.DriverOf(o)),
        Tank = o.TankCapacity is { } cap ? new AvlTank { TankId = o.Id, TankName = "Zbiornik", Capacity = cap } : null,
        HasLocationData = o.SenderId is not null,
    };

    static AvlDriver Driver(SimDriver d) => new() { UserId = d.Id, Login = d.Login, FirstName = d.FirstName, LastName = d.LastName, IsActive = d.IsActive };

    public static List<AvlDriver> Drivers() => SimFleet.Drivers.Select(Driver).ToList();

    public static List<AvlEquipment> Equipment() => SimFleet.Objects.Select(o => new AvlEquipment
    {
        EquipmentId = 5000 + o.Id,
        TrackingObject = TrackingObject(o),
        GpsDeviceType = o.HasCan ? 1 : 0,
        NumberOfFuelProbes = o.Type is 2 or 3 ? 2 : o.TankCapacity is null ? 0 : 1,
        NumberOfAlcoDetectors = o.Type is 4 or 5 ? 1 : 0,
        NumberOfVideoRecorders = o.Type is 3 or 4 ? 1 : 0,
        NumberOfMonitors = o.Type == 4 ? 2 : 0,
        NumberOfCameras = o.Type is 3 or 4 ? 2 : 0,
    }).ToList();

    public static List<AvlArea> Areas() => SimFleet.Areas.Select(a =>
    {
        var dLat = a.HalfSizeKm / 111.32;
        var dLon = a.HalfSizeKm / (111.32 * Math.Cos(a.Latitude * Math.PI / 180));
        (double Lon, double Lat)[] corners =
        [
            (a.Longitude - dLon, a.Latitude - dLat), (a.Longitude + dLon, a.Latitude - dLat),
            (a.Longitude + dLon, a.Latitude + dLat), (a.Longitude - dLon, a.Latitude + dLat), (a.Longitude - dLon, a.Latitude - dLat),
        ];
        return new AvlArea
        {
            AreaId = a.Id,
            AreaName = a.Name,
            Outline = "POLYGON ((" + string.Join(", ", corners.Select(c => string.Create(CultureInfo.InvariantCulture, $"{c.Lon:0.######} {c.Lat:0.######}"))) + "))",
            FillColor = a.FillColor,
            OutlineColor = a.OutlineColor,
            OutlineThickness = 2,
            IsActive = true,
        };
    }).ToList();

    public static List<AvlEventTemplate> EventTemplates(SimObject o) =>
    [
        new() { EventTemplateId = o.Id * 10 + 1, EventType = 4, Entry = true, EventTemplateName = "Wjazd do strefy", Area = Areas()[0] },
        new() { EventTemplateId = o.Id * 10 + 2, EventType = 6, EventTemplateName = "Przekroczenie 90 km/h", IntValue = 90 },
        new() { EventTemplateId = o.Id * 10 + 3, EventType = 7, EventTemplateName = "Przegląd techniczny", DateTimeValue = new DateTime(2027, 3, 1 + o.Id % 28, 0, 0, 0, DateTimeKind.Unspecified) },
        new() { EventTemplateId = o.Id * 10 + 4, EventType = 5, EventTemplateName = "Praca poza godzinami", BeginHour = "18:00", EndHour = "06:00" },
    ];

    // ---------------- Wiadomości ----------------

    /// <summary>Koniec dostępnych danych obiektu: bieżąca chwila, dla obiektu bez łączności — kilka godzin wcześniej.</summary>
    DateTime Until(SimObject o) => o.SenderId is null
        ? SimMotion.Epoch
        : o.Behavior == SimBehavior.Offline ? Now - SimMotion.OfflineFor : Now;

    static TimeSpan Step(SimObject o) => o.Behavior is SimBehavior.Driving or SimBehavior.NoGps ? TimeSpan.FromSeconds(60) : TimeSpan.FromMinutes(5);

    static DateTime Floor(DateTime t, TimeSpan step) => new(t.Ticks - t.Ticks % step.Ticks, DateTimeKind.Unspecified);

    public List<AvlTrackingMessage> LastMessages() => SimFleet.Objects
        .Where(o => o.SenderId is not null)
        .Select(o => Message(o, Floor(Until(o), o.Behavior == SimBehavior.Driving ? TimeSpan.FromSeconds(10) : Step(o))))
        .ToList();

    public List<AvlTrackingMessage> Messages(SimObject o, DateTime from, DateTime to)
    {
        var end = Min(to, Until(o));
        var step = Step(o);
        var list = new List<AvlTrackingMessage>();
        for (var t = Floor(from, step) < from ? Floor(from, step) + step : from; t <= end && list.Count < MaxMessages; t += step)
            list.Add(Message(o, t));
        return list;
    }

    static AvlTrackingMessage Message(SimObject o, DateTime t)
    {
        var (lat, lon, azimuth) = SimMotion.Position(o, t);
        var driving = SimMotion.IsDriving(o, t);
        var engine = SimMotion.IsEngineOn(o, t);
        var speed = SimMotion.Speed(o, t);
        var driver = SimFleet.DriverOf(o);
        var validGps = o.Behavior != SimBehavior.NoGps;
        return new AvlTrackingMessage
        {
            TrackingMessageId = (t - SimMotion.Epoch).TotalSeconds * 1000 + o.Id,
            SenderId = o.SenderId,
            Location = new AvlLocation { Latitude = Math.Round(lat, 6), Longitude = Math.Round(lon, 6) },
            ValidGPS = validGps,
            Satellites = validGps ? 8 + o.Id % 5 : 0,
            // Kurs UniAVL: przeciwnie do ruchu wskazówek zegara, ±180°, zaokrąglony do 45°
            Heading = driving ? UniAvlHeading(azimuth) : 0,
            Speed = speed,
            SpeedMax = driving ? Math.Round(speed * 1.15, 1) : 0,
            Timedate = t,
            IsEngineStarted = o.Type == 19 ? null : engine,
            Sensors = SensorDefinitions(o).Select(s => Sensor(s, SensorValue(o, s, t))).ToList(),
            DriverName = $"{driver.FirstName} {driver.LastName}",
            DriverId = driver.Id.ToString(CultureInfo.InvariantCulture),
            TotalDistanceCAN = o.HasCan ? Math.Round(SimMotion.Odometer(o, t), 1) : null,
            SpeedFromCAN = o.HasCan ? speed : null,
            RPM = o.HasCan ? SimMotion.Rpm(o, t) : null,
            Fuel = o.TankCapacity is { } cap && SimMotion.FuelLevel(o, t) is { } level ? Math.Round(level / cap, 4) : null,
        };
    }

    static double UniAvlHeading(double azimuth)
    {
        var h = -Math.Round(azimuth / 45) * 45;
        h = ((h % 360) + 360) % 360;
        return h > 180 ? h - 360 : h;
    }

    // ---------------- Czujniki ----------------

    sealed record SensorDef(int Id, string Name, int Type, int Order, bool Numeric);

    static List<SensorDef> SensorDefinitions(SimObject o) => o.Type == 19
        ? [new(o.Id * 10 + 1, "Przycisk alarmowy", 2, 1, false)]
        :
        [
            new(o.Id * 10 + 1, "Stacyjka", 1, 1, false),
            new(o.Id * 10 + 2, "Drzwi ", 2, 2, false),
            new(o.Id * 10 + 3, "Temp. zew. [C]: ", 3, 3, true),
        ];

    static JsonElement SensorValue(SimObject o, SensorDef s, DateTime t) => s.Order switch
    {
        _ when s.Numeric => JsonSerializer.SerializeToElement(Math.Round(12 + 6 * Math.Sin((t.TimeOfDay.TotalHours - 9) / 24 * 2 * Math.PI), 1), SimJsonContext.Default.Double),
        1 when o.Type != 19 => SimMotion.IsEngineOn(o, t) ? True : False,
        2 => DoorsOpen(o, t) ? True : False,
        _ => False,
    };

    /// <summary>Drzwi otwarte przez pierwsze 3 minuty postoju.</summary>
    static bool DoorsOpen(SimObject o, DateTime t) =>
        o.Behavior == SimBehavior.Driving && !SimMotion.IsDriving(o, t) && SimMotion.IsDriving(o, t.AddMinutes(-3));

    static AvlSensor Sensor(SensorDef s, JsonElement value) => new()
    {
        SensorId = s.Id,
        SensorName = s.Name,
        SensorType = s.Type,
        Order = s.Order,
        Visible = true,
        Value = value,
    };

    // ---------------- Raport trasy ----------------

    sealed record Segment(bool Ride, DateTime Begin, DateTime End)
    {
        public TimeSpan Length => End - Begin;
    }

    List<Segment> Segments(SimObject o, DateTime from, DateTime to)
    {
        var end = Min(to, Until(o));
        var list = new List<Segment>();
        if (end <= from) return list;
        var start = from;
        foreach (var s in SimMotion.Switches(o, from, end))
        {
            list.Add(new Segment(SimMotion.IsDriving(o, start), start, s));
            start = s;
        }
        list.Add(new Segment(SimMotion.IsDriving(o, start), start, end));
        return list;
    }

    public AvlRoute Route(SimObject o, DateTime from, DateTime to)
    {
        var segments = Segments(o, from, to);
        var driver = SimFleet.DriverOf(o);
        var stages = segments.Select(s => Stage(o, s, $"{driver.FirstName} {driver.LastName}")).ToList();
        var rides = segments.Where(s => s.Ride).ToList();
        var ride = Sum(rides);
        var stop = Sum(segments.Where(s => !s.Ride));
        var distance = rides.Sum(s => SimMotion.Km(o, s.Begin, s.End));
        var fuel = o.TankCapacity is null ? (double?)null : Math.Round(distance * SimMotion.FuelRate(o), 2);
        var doors = segments.Where(s => !s.Ride).Sum(s => Math.Min(3, s.Length.TotalMinutes));
        var end = segments.Count > 0 ? segments[^1].End : from;
        return new AvlRoute
        {
            TrackingObject = TrackingObject(o),
            BeginDate = from,
            EndDate = to,
            RideTime = ride,
            StopTime = stop,
            Distance = Math.Round(distance, 2),
            DistanceGPS = Math.Round(distance * 0.985, 2),
            MaxRPM = rides.Count > 0 && o.HasCan ? (int)Math.Round(1200 + o.SpeedKmh * 1.25 * 15) : null,
            MaxSpeed = rides.Count > 0 ? Math.Round(o.SpeedKmh * 1.25, 1) : 0,
            AverageSpeed = ride.TotalHours > 0 ? Math.Round(distance / ride.TotalHours, 1) : 0,
            FuelUsed = fuel,
            TotalDistanceCAN = o.HasCan ? Math.Round(SimMotion.Odometer(o, end), 1) : null,
            SensorsActivityTime = o.Type == 19 ? null : new Dictionary<string, JsonElement>
            {
                ["Stacyjka"] = Duration(o.Behavior == SimBehavior.NoGps ? stop : ride),
                ["Drzwi "] = Duration(TimeSpan.FromMinutes(doors)),
            },
            SensorsValue = o.Type == 19 ? null : new Dictionary<string, JsonElement>
            {
                ["Temp. zew. [C]: "] = SensorValue(o, SensorDefinitions(o)[2], end),
            },
            Stages = stages,
        };
    }

    static AvlStage Stage(SimObject o, Segment s, string driver)
    {
        var km = s.Ride ? SimMotion.Km(o, s.Begin, s.End) : 0;
        return new AvlStage
        {
            StageType = s.Ride ? 0 : 1,
            BeginDate = s.Begin,
            EndDate = s.End,
            Time = FormatDuration(s.Length),
            Distance = Math.Round(km, 2),
            MaxRPM = o.HasCan ? (s.Ride ? (int)Math.Round(1200 + o.SpeedKmh * 1.25 * 15) : 0) : null,
            MaxSpeed = s.Ride ? Math.Round(o.SpeedKmh * 1.25, 1) : 0,
            AverageSpeed = s.Ride && s.Length.TotalHours > 0 ? Math.Round(km / s.Length.TotalHours, 1) : 0,
            FuelUsed = o.TankCapacity is null ? null : Math.Round(km * SimMotion.FuelRate(o), 2),
            DriverName = driver,
            TotalDistanceCAN = o.HasCan ? Math.Round(SimMotion.Odometer(o, s.End), 1) : null,
            SensorsActivityTime = o.Type == 19 ? null : new Dictionary<string, JsonElement>
            {
                ["Drzwi "] = Duration(s.Ride ? TimeSpan.Zero : TimeSpan.FromMinutes(Math.Min(3, s.Length.TotalMinutes))),
            },
        };
    }

    // ---------------- Paliwo, zdarzenia, styl jazdy ----------------

    public AvlFuelReport? FuelReport(SimObject o, DateTime from, DateTime to)
    {
        if (o.TankCapacity is null) return null;
        var end = Min(to, Until(o));
        var timeData = new List<AvlTimeChartData>();
        var distanceData = new List<AvlDistanceValue>();
        var step = TimeSpan.FromMinutes(5);
        for (var t = from; t <= end; t += step)
        {
            var level = SimMotion.FuelLevel(o, t) ?? 0;
            timeData.Add(new AvlTimeChartData { Argument = t, Value = level });
            distanceData.Add(new AvlDistanceValue { Argument = Math.Round(SimMotion.Km(o, from, t), 2), Value = level, Hint = t });
        }
        return new AvlFuelReport
        {
            FuelUsed = Math.Round(SimMotion.Km(o, from, end) * SimMotion.FuelRate(o), 2),
            Events = Refuels(o, from, end).ToList(),
            TimeChartData = timeData,
            DistanceChartData = new AvlValues<AvlDistanceValue> { Values = distanceData },
        };
    }

    static IEnumerable<AvlEvent> Refuels(SimObject o, DateTime from, DateTime end)
    {
        if (o.TankCapacity is not { } cap) yield break;
        for (var day = from.Date; day <= end; day = day.AddDays(1))
        {
            var t = day + SimMotion.RefuelHour;
            if (t < from || t > end) continue;
            var before = SimMotion.FuelLevel(o, t.AddSeconds(-1)) ?? 0;
            var (lat, lon, _) = SimMotion.Position(o, t);
            yield return new AvlEvent
            {
                Fuel = Math.Round(0.9 * cap - before, 1),
                EventDate = t,
                EventType = 3,
                Description = "Tankowanie",
                Location = new AvlLocation { Latitude = lat, Longitude = lon },
            };
        }
    }

    public List<AvlEvent> Events(SimObject o, DateTime from, DateTime to)
    {
        var end = Min(to, Until(o));
        var list = new List<AvlEvent>(Refuels(o, from, end));
        foreach (var s in Segments(o, from, end).Where(s => !s.Ride && o.Behavior == SimBehavior.Driving && s.Begin > from))
        {
            var (lat, lon, _) = SimMotion.Position(o, s.Begin);
            var location = new AvlLocation { Latitude = lat, Longitude = lon };
            list.Add(new AvlEvent { EventDate = s.Begin, EventType = 5, Description = "Wyłączenie stacyjki", Location = location });
            list.Add(new AvlEvent { EventDate = s.Begin.AddSeconds(20), EventEndDate = s.Begin.AddMinutes(3), EventType = 1, Description = "Otwarcie drzwi", Location = location });
        }
        for (var day = from.Date; day <= end; day = day.AddDays(1))
        {
            var t = day.AddHours(10).AddMinutes(o.Id % 50);
            if (t < from || t > end || !SimMotion.IsDriving(o, t) || o.SpeedKmh < 40) continue;
            var (lat, lon, _) = SimMotion.Position(o, t);
            list.Add(new AvlEvent
            {
                EventDate = t,
                EventEndDate = t.AddSeconds(45),
                EventType = 6,
                Description = string.Create(CultureInfo.InvariantCulture, $"Przekroczenie prędkości: {Math.Round(o.SpeedKmh * 1.25)} km/h"),
                Location = new AvlLocation { Latitude = lat, Longitude = lon },
            });
        }
        return list;
    }

    public AvlDrivingStyle DrivingStyle(SimObject o, DateTime from, DateTime to)
    {
        var rideHours = Sum(Segments(o, from, to).Where(s => s.Ride)).TotalHours;
        double F(int k) => Math.Round(0.02 + 0.25 * SimMotion.Noise(o.Id * 13 + k), 3);
        return new AvlDrivingStyle
        {
            DrivingStyleFeatures = [.. Enumerable.Range(0, 4).Select(k => new AvlDrivingStyleFeature { FeatureType = k, Value = rideHours > 0 ? F(k) : null })],
            SpeedingTime = TimeSpan.FromMinutes(Math.Round(rideHours * 60 * F(0))),
            ExceedRpmTime = o.HasCan ? TimeSpan.FromMinutes(Math.Round(rideHours * 60 * F(1) / 2)) : TimeSpan.Zero,
            NumberOfSuddenBrakings = (int)Math.Round(rideHours * 4 * F(2)),
            NumberOfQuickAccelerations = (int)Math.Round(rideHours * 4 * F(3)),
        };
    }

    // ---------------- Raport floty i karty drogowe ----------------

    public AvlFleetReport FleetReport(SimObject o, DateTime from, DateTime to)
    {
        var days = new List<AvlDailyReport>();
        for (var day = from.Date; day <= to.Date && day <= Now; day = day.AddDays(1))
        {
            var segments = Segments(o, day, day.AddDays(1));
            days.Add(new AvlDailyReport
            {
                DailyReportId = o.Id * 100_000 + (int)(day - SimMotion.Epoch).TotalDays,
                DateTime = day,
                TrackingObject = TrackingObject(o),
                DailyReportRouteDetail = RouteDetail(o, segments, day.AddDays(1)),
                DailyReportFuelDetail = FuelDetail(o, segments),
                DailyReportSensorDetails = new AvlValues<AvlSensorDetail> { Values = SensorDetails(segments) },
            });
        }
        var all = Segments(o, from.Date, to.Date.AddDays(1));
        return new AvlFleetReport
        {
            DailyReports = days,
            DailyReportRouteDetail = RouteDetail(o, all, to.Date.AddDays(1)),
            DailyReportFuelDetail = FuelDetail(o, all),
            DailyReportSensorDetails = SensorDetails(all),
        };
    }

    AvlRouteDetail RouteDetail(SimObject o, List<Segment> segments, DateTime end)
    {
        var rides = segments.Where(s => s.Ride).ToList();
        var ride = Sum(rides);
        var km = rides.Sum(s => SimMotion.Km(o, s.Begin, s.End));
        return new AvlRouteDetail
        {
            RideTime = FormatDuration(ride),
            StopTime = FormatDuration(Sum(segments.Where(s => !s.Ride))),
            Distance = Math.Round(km, 2),
            MaxRPM = o.HasCan && rides.Count > 0 ? (int)Math.Round(1200 + o.SpeedKmh * 1.25 * 15) : null,
            MaxSpeed = rides.Count > 0 ? Math.Round(o.SpeedKmh * 1.25, 1) : 0,
            AverageSpeed = ride.TotalHours > 0 ? Math.Round(km / ride.TotalHours, 1) : 0,
            NumberOfSuddenBrakings = (int)Math.Round(ride.TotalHours * 0.4),
            NumberOfQuickAccelerations = (int)Math.Round(ride.TotalHours * 0.3),
            Odometer = o.HasCan ? Math.Round(SimMotion.Odometer(o, Min(end, Until(o))), 1) : null,
        };
    }

    static AvlFuelDetail? FuelDetail(SimObject o, List<Segment> segments)
    {
        if (o.TankCapacity is null) return null;
        var rides = segments.Where(s => s.Ride).ToList();
        var km = rides.Sum(s => SimMotion.Km(o, s.Begin, s.End));
        var used = km * SimMotion.FuelRate(o);
        var hours = Sum(rides).TotalHours;
        return new AvlFuelDetail
        {
            FuelUsed = Math.Round(used, 2),
            FuelConsumption = km > 0 ? Math.Round(used / km * 100, 2) : null,
            FuelConsumptionInTime = hours > 0 ? Math.Round(used / hours, 2) : null,
        };
    }

    static List<AvlSensorDetail> SensorDetails(List<Segment> segments) =>
    [
        new() { SensorName = "Drzwi", ActivityTime = FormatDuration(TimeSpan.FromMinutes(segments.Where(s => !s.Ride).Sum(s => Math.Min(3, s.Length.TotalMinutes)))) },
        new() { SensorName = "Hamulec", ActivityTime = FormatDuration(Sum(segments.Where(s => s.Ride)) / 12) },
        new() { SensorName = "Bieg neutralny", ActivityTime = FormatDuration(Sum(segments.Where(s => s.Ride)) / 20) },
    ];

    public List<AvlLogbook> Logbooks(DateTime from, DateTime to)
    {
        var list = new List<AvlLogbook>();
        foreach (var o in SimFleet.Objects.Where(o => o.HasLogbook))
        {
            for (var day = from.Date; day <= to.Date && day <= Now.Date; day = day.AddDays(1))
            {
                var trips = Trips(o, day);
                if (trips.Count == 0) continue;
                list.Add(new AvlLogbook
                {
                    LogbookId = LogbookId(o, day),
                    Date = day,
                    TrackingObject = TrackingObject(o),
                    LogbookDetails = new AvlLogbookDetails
                    {
                        BeginOdometer = trips[0].BeginOdometer,
                        EndOdometer = trips[^1].EndOdometer,
                        Distance = trips[^1].EndOdometer - trips[0].BeginOdometer,
                        Driver = Driver(SimFleet.DriverOf(o)),
                    },
                });
            }
        }
        return list;
    }

    public List<AvlLogbookDetails> LogbookDetails(int logbookId)
    {
        var o = SimFleet.Find(logbookId / 100_000);
        if (o is null || !o.HasLogbook) return [];
        return Trips(o, SimMotion.Epoch.AddDays(logbookId % 100_000));
    }

    static int LogbookId(SimObject o, DateTime day) => o.Id * 100_000 + (int)(day - SimMotion.Epoch).TotalDays;

    /// <summary>Wpisy karty drogowej: kursy między 8:00 a 16:00.</summary>
    List<AvlLogbookDetails> Trips(SimObject o, DateTime day)
    {
        string[] aims = ["Dostawa towaru", "Serwis u klienta", "Odbiór materiałów", "Spotkanie handlowe"];
        var driver = Driver(SimFleet.DriverOf(o));
        return Segments(o, day.AddHours(8), day.AddHours(16)).Where(s => s.Ride && s.Length >= TimeSpan.FromMinutes(10)).Select((s, i) =>
        {
            var (bLat, bLon, _) = SimMotion.Position(o, s.Begin);
            var (eLat, eLon, _) = SimMotion.Position(o, s.End);
            var begin = (int)Math.Round(SimMotion.Odometer(o, s.Begin));
            var end = (int)Math.Round(SimMotion.Odometer(o, s.End));
            return new AvlLogbookDetails
            {
                LogbookDetailId = LogbookId(o, day) * 10 + i,
                AimTrip = aims[i % aims.Length],
                BeginTime = s.Begin,
                EndTime = s.End,
                BeginLocation = new AvlLocation { Latitude = bLat, Longitude = bLon },
                EndLocation = new AvlLocation { Latitude = eLat, Longitude = eLon },
                BeginOdometer = begin,
                EndOdometer = end,
                Distance = end - begin,
                Driver = driver,
            };
        }).ToList();
    }

    // ---------------- Pomocnicze ----------------

    static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    static TimeSpan Sum(IEnumerable<Segment> segments) => segments.Aggregate(TimeSpan.Zero, (a, s) => a + s.Length);

    static string FormatDuration(TimeSpan v) => string.Create(CultureInfo.InvariantCulture, $"{(int)v.TotalHours:00}:{v.Minutes:00}:{v.Seconds:00}");

    static JsonElement Duration(TimeSpan v) => JsonSerializer.SerializeToElement(FormatDuration(v), SimJsonContext.Default.String);
}
