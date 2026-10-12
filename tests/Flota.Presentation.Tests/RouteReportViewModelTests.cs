using System.Net;
using Flota.Core.Contracts;
using Flota.Core.Periods;
using Flota.Presentation.Fleet;
using Flota.Presentation.Reports;
using Microsoft.Extensions.Time.Testing;

namespace Flota.Presentation.Tests;

/// <summary>Raport trasy (odpowiednik <c>Components/Reports.razor</c>): parametry, zakładki, punkty trasy, odtwarzanie.</summary>
public sealed class RouteReportViewModelTests : IDisposable
{
    static readonly RouteReport Report = new()
    {
        ObjectId = 1,
        ObjectName = "Ciężarówka",
        HasFuelData = true,
        Stages =
        [
            new RouteStage { Index = 0, Type = StageType.Ride, BeginDate = Data.Now.AddHours(-1), EndDate = Data.Now.AddMinutes(-30) },
            new RouteStage { Index = 1, Type = StageType.Stop, BeginDate = Data.Now.AddMinutes(-30), EndDate = Data.Now },
        ],
    };

    readonly FakeApi _api = new();
    readonly Notifier _notifier = new();
    readonly List<Notice> _notices = [];
    readonly FakeTimeProvider _clock = new(new DateTimeOffset(Data.Now, TimeSpan.Zero));
    readonly FleetViewModel _fleet;
    readonly RouteReportViewModel _vm;

    public RouteReportViewModelTests()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _notifier.Raised += (_, n) => _notices.Add(n);
        _api.On(HttpMethod.Get, "fleet/snapshot", Data.Snapshot(Data.Object(1, "Ciężarówka", Data.Position(Data.Now)), Data.Object(2, "Bus", Data.Position(Data.Now))), FlotaJsonContext.Default.FleetSnapshot)
            .On(HttpMethod.Get, "objects/1/route", Report, FlotaJsonContext.Default.RouteReport)
            .On(HttpMethod.Post, "objects/1/waypoints", Waypoints(3), FlotaJsonContext.Default.WaypointsResponse)
            .On(HttpMethod.Get, "objects/1/fuel", new FuelReport { Fueled = 40 }, FlotaJsonContext.Default.FuelReport);
        var client = _api.Client(Data.Tokens);
        _fleet = new FleetViewModel(client, new FakeLiveFeed(), new MemorySnapshotStore(), _notifier, _clock);
        _vm = new RouteReportViewModel(client, _fleet, _notifier, _clock);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _api.Dispose();
    }

    static WaypointsResponse Waypoints(int count) => new(
        ["Drzwi"],
        Enumerable.Range(0, count).Select(i => new Waypoint { Id = i, Position = Data.Position(Data.Now.AddMinutes(-count + i), lat: 52 + i * 0.001) }).ToList());

    Uri LastRequest(string path) => _api.Requests.Last(r => r.RequestUri!.AbsolutePath.StartsWith("/" + path, StringComparison.Ordinal)).RequestUri!;

    [Fact]
    public void Defaults_LastHour()
    {
        Assert.Equal(PeriodType.LastHour, _vm.Period);
        Assert.Equal(Data.Now.AddHours(-1), _vm.From);
        Assert.Equal(Data.Now, _vm.To);
        Assert.False(_vm.IsCustomPeriod);

        _vm.Period = PeriodType.NonStandard;
        Assert.True(_vm.IsCustomPeriod);
    }

    [Fact]
    public void StopCriteria_AtLeastOneEnabled()
    {
        Assert.True(_vm.IgnitionOffEnabled);
        Assert.False(_vm.StopEnabled);

        _vm.IgnitionOffEnabled = false;
        Assert.True(_vm.StopEnabled);

        _vm.StopEnabled = false;
        Assert.True(_vm.IgnitionOffEnabled);
    }

    [Fact]
    public async Task CreatedAfterSelection_TakesSelectedObject()
    {
        // Panel raportu tworzony przy pierwszym otwarciu — obiekt zaznaczony wcześniej na liście
        await _fleet.RefreshAsync(TestContext.Current.CancellationToken);
        _fleet.Select(_fleet.Find(2)!, center: false);
        using var late = new RouteReportViewModel(_api.Client(Data.Tokens), _fleet, _notifier, _clock);
        Assert.Equal(2, late.ObjectId);
    }

    [Fact]
    public async Task Generate_WithoutObject_AsksToPickObject()
    {
        await _vm.GenerateAsync(showRoute: false);
        Assert.Empty(_api.Requests);
        Assert.Equal(NoticeSeverity.Informational, Assert.Single(_notices).Severity);
    }

    [Fact]
    public async Task Generate_QueryFromCriteria()
    {
        _vm.ObjectId = 1;
        _vm.StopEnabled = true;
        _vm.StopTime = "00:05:00";
        _vm.MinSpeed = 7;
        _vm.IgnitionOffTime = "bez sensu";

        await _vm.GenerateAsync(showRoute: false);

        var query = Uri.UnescapeDataString(LastRequest("objects/1/route").Query);
        Assert.Contains("from=2026-09-27T11:00:00&to=2026-09-27T12:00:00", query, StringComparison.Ordinal);
        Assert.Contains("stopTime=00:05:00&minSpeed=7", query, StringComparison.Ordinal);
        // Niepoprawny czas — wartość domyślna kryterium
        Assert.Contains("ignitionOffTime=00:01:00", query, StringComparison.Ordinal);
        Assert.True(_vm.Generated);
        Assert.Equal(RouteTab.Route, _vm.SelectedTab);
        Assert.True(_vm.HasFuel);
        Assert.Equal("Ciężarówka", _vm.ObjectName);
    }

    [Fact]
    public async Task Generate_StopCriterionDisabled_NoStopTimeAndZeroSpeed()
    {
        _vm.ObjectId = 1;
        _vm.MinSpeed = 7;
        await _vm.GenerateAsync(showRoute: false);
        var query = LastRequest("objects/1/route").Query;
        Assert.DoesNotContain("stopTime", query, StringComparison.Ordinal);
        Assert.DoesNotContain("minSpeed", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateWithRoute_AllStagesWaypointsAndMapTrace()
    {
        await _fleet.RefreshAsync(TestContext.Current.CancellationToken);
        _vm.ObjectId = 1;

        await _vm.GenerateAsync(showRoute: true);

        Assert.Equal(RouteTab.Waypoints, _vm.SelectedTab);
        Assert.Equal(2, _vm.SelectedStages.Count);
        Assert.Contains("\"type\":\"Stop\"", _api.Bodies.Last(), StringComparison.Ordinal);
        Assert.Equal(3, _vm.Waypoints!.Points.Count);
        Assert.Equal(0, _vm.CurrentWaypoint);
        Assert.Equal(TraceMode.Report, _fleet.TraceMode);
        Assert.Equal(3, _fleet.Trace.Count);
        Assert.Equal(1, _fleet.SelectedObject?.Id);
    }

    [Fact]
    public async Task Tabs_LoadedOnce()
    {
        _vm.ObjectId = 1;
        await _vm.GenerateAsync(showRoute: false);
        var changed = new List<RouteTab>();
        _vm.TabDataChanged += (_, t) => changed.Add(t);

        await _vm.LoadTabAsync(RouteTab.Fuel);
        await _vm.LoadTabAsync(RouteTab.Fuel);

        Assert.Equal(1, _api.Count("objects/1/fuel"));
        Assert.Equal(40, _vm.Fuel!.Fueled);
        Assert.Equal([RouteTab.Fuel], changed);
    }

    [Fact]
    public async Task Tab_Error_NotifiesAndAllowsRetry()
    {
        _api.On(HttpMethod.Get, "objects/1/events", HttpStatusCode.BadGateway);
        _vm.ObjectId = 1;
        await _vm.GenerateAsync(showRoute: false);

        await _vm.LoadTabAsync(RouteTab.Events);
        Assert.Equal(NoticeSeverity.Error, _notices.Last().Severity);
        Assert.False(_vm.IsBusy);

        _api.On(HttpMethod.Get, "objects/1/events", (IReadOnlyList<ObjectEvent>)[], FlotaJsonContext.Default.IReadOnlyListObjectEvent);
        await _vm.LoadTabAsync(RouteTab.Events);
        Assert.Equal(2, _api.Count("objects/1/events"));
    }

    [Fact]
    public async Task SetSelectedStages_ReloadsWaypointsOnNextOpen()
    {
        _vm.ObjectId = 1;
        await _vm.GenerateAsync(showRoute: true);

        _vm.SetSelectedStages([Report.Stages[1]]);
        Assert.Null(_vm.Waypoints);
        await _vm.LoadTabAsync(RouteTab.Waypoints);

        Assert.Equal(2, _api.Count("objects/1/waypoints"));
        Assert.DoesNotContain("\"type\":\"Ride\"", _api.Bodies.Last(), StringComparison.Ordinal);

        _vm.SetSelectedStages([]);
        Assert.Equal(TraceMode.None, _fleet.TraceMode);
    }

    [Fact]
    public async Task Navigation_FirstBackForwardLast()
    {
        _vm.ObjectId = 1;
        await _vm.GenerateAsync(showRoute: true);

        Assert.False(_vm.CanGoBack);
        Assert.True(_vm.CanGoForward);
        _vm.Last();
        Assert.Equal(2, _vm.CurrentWaypoint);
        Assert.False(_vm.CanGoForward);
        _vm.Back();
        Assert.Equal(1, _vm.CurrentWaypoint);
        _vm.Forward();
        _vm.Forward();
        Assert.Equal(2, _vm.CurrentWaypoint);
        _vm.First();
        Assert.Equal(0, _vm.CurrentWaypoint);
        Assert.Equal(0, _fleet.TraceCurrentIndex);
    }

    [Fact]
    public async Task ManyPoints_TraceOnlyCurrentPosition()
    {
        _api.On(HttpMethod.Post, "objects/1/waypoints", Waypoints(RouteReportViewModel.MaxDrawnPoints + 1), FlotaJsonContext.Default.WaypointsResponse);
        _vm.ObjectId = 1;
        await _vm.GenerateAsync(showRoute: true);

        Assert.Equal(NoticeSeverity.Informational, _notices.Last().Severity);
        Assert.Single(_fleet.Trace);
        _vm.Last();
        Assert.Equal(_vm.Waypoints!.Points[^1].Position, Assert.Single(_fleet.Trace));
    }

    [Fact]
    public async Task Playback_AdvancesEveryThreeSeconds()
    {
        _vm.ObjectId = 1;
        await _vm.GenerateAsync(showRoute: true);

        var play = _vm.PlayAsync();
        Assert.True(_vm.IsPlaying);
        Assert.False(_vm.CanGoForward);
        Assert.Equal(0, _vm.CurrentWaypoint);

        _clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(1, _vm.CurrentWaypoint);

        _vm.Stop();
        await play;
        Assert.False(_vm.IsPlaying);
        Assert.Equal(1, _vm.CurrentWaypoint);
        Assert.True(_vm.CanGoForward);
    }

    [Fact]
    public async Task ObjectChange_InvalidatesReport()
    {
        _vm.ObjectId = 1;
        await _vm.GenerateAsync(showRoute: false);
        Assert.True(_vm.Generated);

        _vm.ObjectId = 2;
        Assert.False(_vm.Generated);
    }

    [Fact]
    public async Task FleetSelection_SetsReportObject()
    {
        await _fleet.RefreshAsync(TestContext.Current.CancellationToken);
        _fleet.Select(_fleet.Find(2));
        Assert.Equal(2, _vm.ObjectId);
    }
}
