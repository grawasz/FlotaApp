using Flota.Client;
using Flota.Core.Contracts;
using Flota.Core.Reports;
using Flota.Core.Status;

namespace Flota.Api.Tests;

/// <summary>
/// Punkty końcowe Flota.Api (T2.9) wywoływane klientem <see cref="FlotaApiClient"/> na danych symulatora UniAVL.
/// Wartości oczekiwane wynikają z modelu ruchu symulatora: obiekt 101 jeździ w cyklu 40 min jazdy i 15 min postoju
/// z prędkością 42 km/h i zużyciem 0,10 l/km; 27.09.2026 cykl zaczyna się o 7:25, 8:20, 9:15, …
/// </summary>
public sealed class ApiEndpointsTests(SimulatorFixture sim) : IClassFixture<SimulatorFixture>
{
    static readonly DateTime Day = SimulatorFixture.Now.Date;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    static RouteQuery Query(int fromHour, int toHour) => new()
    {
        From = Day.AddHours(fromHour),
        To = Day.AddHours(toHour),
        StopTime = TimeSpan.FromMinutes(2),
        IgnitionOffTime = TimeSpan.FromMinutes(1),
        MinSpeed = 5,
    };

    [Fact]
    public async Task Snapshot_ObjectsPositionsAndAreas()
    {
        using var api = await sim.LoginAsync();
        var snapshot = await api.GetSnapshotAsync(Ct);

        Assert.Equal(SimulatorFixture.Now.AddHours(0), snapshot.Objects.Single(o => o.Id == 101).Position!.Time);
        Assert.Equal(12, snapshot.Objects.Count);
        Assert.Equal("Firma demonstracyjna", snapshot.User.CompanyName);
        Assert.Equal(3, snapshot.Areas.Count);
        Assert.All(snapshot.Areas, a => Assert.Equal(5, a.Outline.Count));

        TrackingObjectStatus Status(int id) => snapshot.Objects.Single(o => o.Id == id).StatusAt(SimulatorFixture.Now);
        Assert.Equal(TrackingObjectStatus.Move, Status(101));
        Assert.Equal(TrackingObjectStatus.Stop, Status(104));
        Assert.Equal(TrackingObjectStatus.InvalidGps, Status(107));
        Assert.Equal(TrackingObjectStatus.Outdated, Status(108));
        Assert.Equal(TrackingObjectStatus.Unknown, Status(112));

        var truck = snapshot.Objects.Single(o => o.Id == 103);
        Assert.Equal("GPS CAN", truck.DeviceName);
        Assert.Equal("Volvo FH 500", truck.MakeModel);
        Assert.NotNull(truck.Position!.FuelLevel);
    }

    [Fact]
    public async Task Route_StagesAndTotals()
    {
        using var api = await sim.LoginAsync();
        var report = await api.GetRouteAsync(101, Query(8, 12), Ct);

        Assert.NotNull(report);
        Assert.Equal(TimeSpan.FromMinutes(165), report.RideTime);
        Assert.Equal(TimeSpan.FromMinutes(75), report.StopTime);
        Assert.Equal(115.5, report.Distance);
        Assert.Equal(10, report.FuelConsumption!.Value, 3);
        Assert.True(report.HasFuelData);
        Assert.Equal(10, report.Stages.Count);
        Assert.Equal([StageType.Ride, StageType.Stop], report.Stages.Take(2).Select(s => s.Type));
        Assert.Equal(Enumerable.Range(0, 10), report.Stages.Select(s => s.Index));
        Assert.Equal(["TotalDistanceCAN", "FuelUsed", "FuelUsage", "S:Drzwi "], report.StageColumns.Select(c => c.Name));
        Assert.All(report.Stages, s => Assert.Equal(report.StageColumns.Count, s.Values.Count));
        Assert.Contains(report.SensorActivity, a => a.Name == "Drzwi");
        Assert.DoesNotContain(report.SensorActivity, a => a.Name == "Stacyjka");
    }

    [Fact]
    public async Task Route_ObjectWithoutTank_NoFuelData()
    {
        using var api = await sim.LoginAsync();
        var report = await api.GetRouteAsync(110, Query(8, 12), Ct);
        Assert.NotNull(report);
        Assert.False(report.HasFuelData);
        Assert.Null(report.FuelUsed);
    }

    [Fact]
    public async Task Waypoints_PointsAndSensorColumns()
    {
        using var api = await sim.LoginAsync();
        var response = await api.GetWaypointsAsync(101, [new StageWindow(StageType.Ride, Day.AddHours(8).AddMinutes(20), Day.AddHours(9))], Ct);

        Assert.Equal(41, response.Points.Count);
        Assert.Equal(["Stacyjka", "Drzwi", "Temp. zew. [C]:"], response.SensorColumns);
        Assert.All(response.Points, p => Assert.Equal(3, p.SensorValues.Count));
        Assert.Equal(Enumerable.Range(0, 41), response.Points.Select(p => p.Id));
        Assert.All(response.Points, p => Assert.True(p.Position.ValidGps));
    }

    [Fact]
    public async Task Trace_OrderedValidPositions()
    {
        using var api = await sim.LoginAsync();
        var trace = await api.GetTraceAsync(101, Day.AddHours(8), Day.AddHours(9), Ct);
        Assert.Equal(61, trace.Count);
        Assert.Equal(trace.OrderBy(p => p.Time).Select(p => p.Time), trace.Select(p => p.Time));

        // Obiekt bez poprawnej pozycji GPS — brak punktów śladu
        Assert.Empty(await api.GetTraceAsync(107, Day.AddHours(8), Day.AddHours(9), Ct));
    }

    [Fact]
    public async Task Fuel_RefuelingAndDownsampledSeries()
    {
        using var api = await sim.LoginAsync();
        var fuel = await api.GetFuelAsync(101, Day.AddDays(-1), Day.AddHours(12), Ct);

        Assert.NotNull(fuel);
        Assert.Equal(2, fuel.Refueling.Count);
        Assert.Empty(fuel.Defueling);
        Assert.Equal(fuel.Refueling.Sum(r => r.Fuel), fuel.Fueled);
        Assert.Equal(200, fuel.TimeSeries.Count);
        Assert.Equal(200, fuel.DistanceSeries.Count);
        Assert.Equal(fuel.TimeSeries[0].Value, fuel.StartLevel);
        Assert.Equal(fuel.TimeSeries[^1].Value, fuel.FinalLevel);
    }

    [Fact]
    public async Task Fuel_ObjectWithoutTank_NotFound()
    {
        using var api = await sim.LoginAsync();
        Assert.Null(await api.GetFuelAsync(110, Day.AddHours(8), Day.AddHours(12), Ct));
    }

    [Fact]
    public async Task Events_NewestFirst()
    {
        using var api = await sim.LoginAsync();
        var events = await api.GetEventsAsync(101, Day.AddHours(8), Day.AddHours(12), Ct);

        // Postoje rozpoczęte o 8:05, 9:00, 9:55, 10:50 i 11:45: wyłączenie stacyjki i otwarcie drzwi
        Assert.Equal(10, events.Count);
        Assert.Equal(events.OrderByDescending(e => e.Date).Select(e => e.Date), events.Select(e => e.Date));
        Assert.Equal(5, events.Count(e => e.EventType == 5));
        Assert.Equal(5, events.Count(e => e.EventType == 1));
        Assert.All(events, e => Assert.NotNull(e.Latitude));
    }

    [Fact]
    public async Task DrivingStyle_FeaturesWithScores()
    {
        using var api = await sim.LoginAsync();
        var style = await api.GetDrivingStyleAsync(101, new DrivingStyleQuery { Route = Query(8, 12), Speeding = 50, ExceedRpm = 2000, SuddenBraking = 1, QuickAcceleration = 1 }, Ct);

        Assert.NotNull(style);
        Assert.Equal([0, 1, 2, 3], style.Features.Select(f => f.Type));
        Assert.Equal(Enumerable.Range(0, 4).Select(ReportRules.DrivingFeatureName), style.Features.Select(f => f.Name));
        Assert.All(style.Features, f => Assert.InRange(f.Value!.Value, 0, 100));
    }

    [Fact]
    public async Task StageSummary_SameStagesAsRoute()
    {
        using var api = await sim.LoginAsync();
        var q = Query(8, 12);
        var route = await api.GetRouteAsync(101, q, Ct);
        var summary = await api.GetStageSummaryAsync(101, q, Ct);

        Assert.Equal(route!.Stages.Select(s => (s.Index, s.Type, s.BeginDate)), summary.Select(s => (s.Index, s.Type, s.BeginDate)));
    }

    [Fact]
    public async Task FleetReport_SummaryAndDailyDetails()
    {
        using var api = await sim.LoginAsync();
        var report = await api.GetFleetReportAsync(new FleetReportRequest([101, 103, 110], Day.AddDays(-2), Day.AddDays(-1)), Ct);

        Assert.Equal([101, 103, 110], report.Summary.Select(r => r.ObjectId));
        Assert.All(report.Summary, r => Assert.Null(r.Date));
        Assert.Equal(6, report.Details.Count);
        Assert.Equal(report.Details.OrderBy(d => d.Date).Select(d => d.Date), report.Details.Select(d => d.Date));
        Assert.Null(report.Summary.Single(r => r.ObjectId == 110).FuelUsed);

        var van = report.Summary.Single(r => r.ObjectId == 101);
        Assert.Equal(report.Details.Where(d => d.ObjectId == 101).Sum(d => d.Distance!.Value), van.Distance!.Value, 1);
        Assert.Equal("Renault Trafic", van.MakeModel);
        Assert.False(string.IsNullOrEmpty(van.Doors));
    }

    [Fact]
    public async Task EcoDriving_RankingByScore()
    {
        using var api = await sim.LoginAsync();
        var rows = await api.GetEcoDrivingAsync(new EcoDrivingRequest([101, 102, 103, 105], Day.AddDays(-1), Day), Ct);

        Assert.Equal([1, 2, 3, 4], rows.Select(r => r.Position));
        Assert.Equal(rows.OrderByDescending(r => r.Score).Select(r => r.Score), rows.Select(r => r.Score));
        Assert.All(rows, r => Assert.False(string.IsNullOrEmpty(r.RegistrationNumber)));
    }

    [Fact]
    public async Task Logbooks_ListAndEntries()
    {
        using var api = await sim.LoginAsync();
        var logbooks = await api.GetLogbooksAsync(Day.AddDays(-1), Day, Ct);

        // Karty drogowe prowadzą obiekty 101 i 102
        Assert.Equal(4, logbooks.Count);
        Assert.Equal(logbooks.OrderByDescending(l => l.Date).Select(l => l.Date), logbooks.Select(l => l.Date));

        var entries = await api.GetLogbookEntriesAsync(logbooks[^1].Id, Ct);
        Assert.NotEmpty(entries);
        Assert.Equal(entries.OrderBy(e => e.BeginTime).Select(e => e.BeginTime), entries.Select(e => e.BeginTime));
        Assert.All(entries, e => Assert.Equal(e.EndOdometer - e.BeginOdometer, e.Distance));
        Assert.Equal(logbooks[^1].BeginOdometer, entries[0].BeginOdometer);
    }

    [Fact]
    public async Task Dictionaries_DriversEquipmentTemplates()
    {
        using var api = await sim.LoginAsync();

        var drivers = await api.GetDriversAsync(Ct);
        Assert.Equal(8, drivers.Count);
        Assert.Equal("Kamiński", drivers[0].LastName);
        Assert.Equal("Zieliński", drivers[^1].LastName);
        Assert.Single(drivers, d => !d.IsActive);

        var equipment = await api.GetEquipmentAsync(refresh: false, Ct);
        Assert.Equal(12, equipment.Count);
        Assert.Equal(equipment, await api.GetEquipmentAsync(refresh: true, Ct));
        Assert.Equal(2, equipment.Single(e => e.ObjectId == 103).FuelProbes);

        var templates = await api.GetEventTemplatesAsync([101, 103], Ct);
        Assert.Equal(8, templates.Count);
        Assert.Equal(2, templates.Count(t => t.IsReminder));
        Assert.Equal("Baza Warszawa", templates.First(t => t.EventType == 4).AreaName);
    }

    [Fact]
    public async Task Settings_SaveAndRead()
    {
        using var api = await sim.LoginAsync();
        await api.SaveSettingsAsync(new UserSettings(52.1, 21.2, 12), Ct);
        Assert.Equal(new UserSettings(52.1, 21.2, 12), await api.GetSettingsAsync(Ct));
        await api.SaveSettingsAsync(new UserSettings(null, null, null), Ct);
        Assert.Equal(new UserSettings(null, null, null), await api.GetSettingsAsync(Ct));
    }

    [Fact]
    public async Task ExportXlsx_ReturnsWorkbook()
    {
        using var api = await sim.LoginAsync();
        var bytes = await api.ExportXlsxAsync(new ExportTable("Arkusz", [new ExportColumn("Nazwa", ExportColumnKind.Text)], [["Renault Trafic"]]), Ct);
        Assert.Equal("PK"u8.ToArray(), bytes.Take(2));
    }
}
