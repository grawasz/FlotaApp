using System.Net;
using Flota.Client;
using Flota.Core.Contracts;
using Flota.Core.Status;
using Flota.Presentation.Fleet;
using Microsoft.Extensions.Time.Testing;

namespace Flota.Presentation.Tests;

/// <summary>Stan floty: start z pamięci lokalnej, odświeżenie, lista (filtr, sortowanie), zaznaczenie, ślad, widok początkowy.</summary>
public sealed class FleetViewModelTests : IDisposable
{
    static readonly FleetSnapshot Snapshot = Data.Snapshot(
        Data.Object(1, "Ciężarówka", Data.Position(Data.Now, address: Data.Address("Warszawa")), "WX 111"),
        Data.Object(2, "autobus", Data.Position(Data.Now.AddMinutes(-5), engine: false, address: Data.Address("Kraków")), "KR 222"),
        Data.Object(3, "Bus", Data.Position(Data.Now.AddHours(-3), address: Data.Address("Gdańsk")), "GD 333"),
        Data.Object(4, "Radiotelefon"));

    readonly FakeApi _api = new();
    readonly FakeLiveFeed _feed = new();
    readonly MemorySnapshotStore _store = new();
    readonly Notifier _notifier = new();
    readonly List<Notice> _notices = [];
    readonly FakeTimeProvider _clock = new(new DateTimeOffset(Data.Now, TimeSpan.Zero));
    readonly FleetViewModel _vm;

    public FleetViewModelTests()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _notifier.Raised += (_, n) => _notices.Add(n);
        _api.On(HttpMethod.Get, "fleet/snapshot", Snapshot, FlotaJsonContext.Default.FleetSnapshot)
            .On(HttpMethod.Get, "me/settings", new UserSettings(52.1, 21.1, 11), FlotaJsonContext.Default.UserSettings);
        _vm = new FleetViewModel(_api.Client(Data.Tokens), _feed, _store, _notifier, _clock);
    }

    public void Dispose() => _api.Dispose();

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    IEnumerable<int> Listed => _vm.Objects.Select(o => o.Id);

    [Fact]
    public async Task LoadAsync_CachedSnapshotFirst_ThenRefreshAndLiveFeed()
    {
        _store.Stored = Data.Snapshot(Data.Object(9, "Z pamięci", Data.Position(Data.Now.AddDays(-1))));
        var counts = new List<int>();
        var stale = new List<bool>();
        _vm.ObjectsReset += (_, _) => counts.Add(_vm.AllObjects.Count);
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FleetViewModel.IsStale)) stale.Add(_vm.IsStale);
        };

        await _vm.LoadAsync(Ct);

        Assert.Equal([1, 4], counts);
        Assert.Equal([true, false], stale);
        Assert.Null(_vm.Find(9));
        Assert.Equal(4, _store.Stored!.Objects.Count);
        Assert.Equal(1, _feed.Starts);
        Assert.Equal(LiveConnectionState.Connected, _vm.LiveState);
        Assert.Equal("Firma testowa", _vm.User?.CompanyName);
    }

    [Fact]
    public async Task Refresh_ServerError_NotifiesAndKeepsState()
    {
        await _vm.RefreshAsync(Ct);
        _api.On(HttpMethod.Get, "fleet/snapshot", HttpStatusCode.BadGateway);

        await _vm.RefreshAsync(Ct);

        Assert.False(_vm.IsLoading);
        Assert.Equal(4, _vm.AllObjects.Count);
        Assert.Equal(NoticeSeverity.Error, Assert.Single(_notices).Severity);
    }

    [Fact]
    public async Task DefaultSort_StatusThenName()
    {
        await _vm.RefreshAsync(Ct);
        // W ruchu, Zatrzymany, Nieaktualny, Nieznany
        Assert.Equal([1, 2, 3, 4], Listed);
        Assert.Equal(TrackingObjectStatus.Outdated, _vm.Find(3)!.Status);
    }

    [Fact]
    public async Task SetSort_ByName_SecondClickReverses()
    {
        await _vm.RefreshAsync(Ct);
        _vm.SetSort(ObjectSort.Name);
        Assert.Equal([2, 3, 1, 4], Listed);
        Assert.False(_vm.SortDescending);

        _vm.SetSort(ObjectSort.Name);
        Assert.True(_vm.SortDescending);
        Assert.Equal([4, 1, 3, 2], Listed);
    }

    [Fact]
    public async Task SetSort_ByLocation()
    {
        await _vm.RefreshAsync(Ct);
        _vm.SetSort(ObjectSort.Location);
        // Obiekt bez pozycji (pusta lokalizacja) pierwszy
        Assert.Equal([4, 3, 2, 1], Listed);
    }

    [Theory]
    [InlineData("bus", new[] { 2, 3 })]
    [InlineData("WX", new[] { 1 })]
    [InlineData("kraków", new[] { 2 })]
    [InlineData("  ", new[] { 1, 2, 3, 4 })]
    public async Task FilterText_NameRegistrationOrLocation(string filter, int[] expected)
    {
        await _vm.RefreshAsync(Ct);
        _vm.FilterText = filter;
        Assert.Equal(expected, Listed);
    }

    [Fact]
    public async Task LivePositions_UpdateObjectsAndResort()
    {
        await _vm.RefreshAsync(Ct);
        var updated = new List<int>();
        _vm.ObjectsUpdated += (_, items) => updated.AddRange(items.Select(i => i.Id));

        _feed.Raise(new PositionChanged(3, Data.Position(Data.Now.AddMinutes(1), engine: true)), new PositionChanged(99, Data.Position(Data.Now)));

        Assert.Equal([3], updated);
        Assert.Equal(TrackingObjectStatus.Move, _vm.Find(3)!.Status);
        // Dwa obiekty w ruchu — kolejność według nazwy
        Assert.Equal([3, 1, 2, 4], Listed);
    }

    [Fact]
    public async Task LiveEvents_PostedToUiContext()
    {
        var ui = new QueueContext();
        _vm.AttachUiContext(ui);
        await _vm.RefreshAsync(Ct);

        _feed.Raise(new PositionChanged(2, Data.Position(Data.Now.AddMinutes(1))));
        _feed.Raise(new AddressResolved(2, Data.Now.AddMinutes(1), Data.Address("Tarnów")));
        Assert.Equal(2, ui.Posted.Count);
        Assert.Equal("Kraków", _vm.Find(2)!.LocationText);

        ui.RunAll();
        Assert.Equal("Tarnów", _vm.Find(2)!.LocationText);
    }

    [Fact]
    public async Task Select_CentersMapAndShowsObject()
    {
        await _vm.RefreshAsync(Ct);
        var item = _vm.Find(2)!;
        item.IsVisibleOnMap = false;
        (double, double, double?)? center = null;
        _vm.CenterRequested += (_, c) => center = c;

        _vm.Select(item);

        Assert.Same(item, _vm.SelectedObject);
        Assert.True(item.IsSelected);
        Assert.True(item.IsVisibleOnMap);
        Assert.Equal((52.23, 21.01, (double?)null), center);

        _vm.Select(_vm.Find(1));
        Assert.False(item.IsSelected);
    }

    [Fact]
    public async Task Select_ObjectWithoutPosition_NoCentering()
    {
        await _vm.RefreshAsync(Ct);
        var centered = false;
        _vm.CenterRequested += (_, _) => centered = true;
        _vm.Select(_vm.Find(4));
        Assert.False(centered);
        Assert.False(_vm.Find(4)!.IsVisibleOnMap);
    }

    [Fact]
    public async Task ZoomTo_Level18()
    {
        await _vm.RefreshAsync(Ct);
        double? zoom = null;
        _vm.CenterRequested += (_, c) => zoom = c.Zoom;
        _vm.ZoomTo(_vm.Find(1)!);
        Assert.Equal(18, zoom);
    }

    [Fact]
    public async Task SetAllVisible_OnlyObjectsWithPosition()
    {
        await _vm.RefreshAsync(Ct);
        _vm.SetAllVisible(false);
        Assert.All(_vm.AllObjects, o => Assert.False(o.IsVisibleOnMap));
        _vm.SetAllVisible(true);
        Assert.Equal([1, 2, 3], _vm.AllObjects.Where(o => o.IsVisibleOnMap).Select(o => o.Id).Order());
    }

    [Fact]
    public async Task Tracking_LoadsTraceFromLastPositionMinus5Minutes()
    {
        IReadOnlyList<ObjectPosition> trace = [Data.Position(Data.Now.AddMinutes(-4)), Data.Position(Data.Now)];
        _api.On(HttpMethod.Get, "objects/1/trace", trace, FlotaJsonContext.Default.IReadOnlyListObjectPosition);
        await _vm.RefreshAsync(Ct);
        _vm.Select(_vm.Find(1));

        await _vm.ToggleTrackingAsync();

        Assert.True(_vm.IsTracking);
        Assert.Equal(TraceMode.Tracking, _vm.TraceMode);
        Assert.Equal(2, _vm.Trace.Count);
        Assert.Equal(1, _vm.TraceCurrentIndex);
        var query = _api.Requests.Last().RequestUri!.Query;
        Assert.Contains("from=2026-09-27T11%3A55%3A00", query, StringComparison.Ordinal);
        Assert.Contains("to=2026-09-27T12%3A00%3A00", query, StringComparison.Ordinal);

        // Nowa pozycja obiektu śledzonego — ponowne wczytanie śladu
        _feed.Raise(new PositionChanged(1, Data.Position(Data.Now.AddSeconds(10))));
        await Task.Yield();
        Assert.Equal(2, _api.Count("objects/1/trace"));

        await _vm.ToggleTrackingAsync();
        Assert.Equal(TraceMode.None, _vm.TraceMode);
        Assert.Empty(_vm.Trace);
    }

    [Fact]
    public async Task ReportTrace_StopsTracking()
    {
        await _vm.RefreshAsync(Ct);
        _vm.IsTracking = true;
        _vm.SetReportTrace([Data.Position(Data.Now.AddHours(-1)), Data.Position(Data.Now)], 0);

        Assert.False(_vm.IsTracking);
        Assert.Equal(TraceMode.Report, _vm.TraceMode);
        Assert.Equal(0, _vm.TraceCurrentIndex);

        _vm.SetReportTrace([], -1);
        Assert.Equal(TraceMode.None, _vm.TraceMode);
    }

    [Fact]
    public async Task DefaultView_LoadGoSaveDelete()
    {
        _api.On(HttpMethod.Put, "me/settings", HttpStatusCode.NoContent);
        var centers = new List<(double, double, double?)>();
        _vm.CenterRequested += (_, c) => centers.Add(c);

        await _vm.LoadAsync(Ct);
        await Task.Yield();
        Assert.Equal((52.1, 21.1, (double?)11), centers.Last());

        await _vm.SaveDefaultViewAsync(50, 20, 9);
        Assert.Equal(new UserSettings(50, 20, 9), _vm.Settings);
        Assert.Contains("\"defaultLatitude\":50", _api.Bodies.Last(), StringComparison.Ordinal);
        Assert.Equal(NoticeSeverity.Success, _notices.Last().Severity);

        await _vm.DeleteDefaultViewAsync();
        Assert.Equal(new UserSettings(null, null, null), _vm.Settings);
        centers.Clear();
        _vm.GoToDefaultView();
        Assert.Empty(centers);
    }

    [Fact]
    public async Task ActiveAreas_SortedByNameIgnoringCase()
    {
        await _vm.RefreshAsync(Ct);
        Assert.Equal(["baza", "Zakład"], _vm.ActiveAreas.Select(a => a.Name));

        (double Lat, double Lon, double?)? center = null;
        _vm.CenterRequested += (_, c) => center = c;
        _vm.CenterOnArea(_vm.Areas[0]);
        Assert.Equal(52.1, center!.Value.Lat, 6);
        Assert.Equal(21.1, center.Value.Lon, 6);
    }

    [Fact]
    public async Task RefreshStatuses_AfterTimePasses()
    {
        await _vm.RefreshAsync(Ct);
        _clock.Advance(TimeSpan.FromMinutes(61));
        _vm.RefreshStatuses();
        Assert.Equal(TrackingObjectStatus.Outdated, _vm.Find(1)!.Status);
        // Trzy obiekty nieaktualne — kolejność według nazwy, obiekt bez pozycji na końcu
        Assert.Equal([2, 3, 1, 4], Listed);
    }

    [Fact]
    public async Task ClearAsync_StopsFeedAndRemovesLocalState()
    {
        await _vm.LoadAsync(Ct);
        _vm.Select(_vm.Find(1));
        await _vm.ClearAsync();

        Assert.Equal(1, _feed.Stops);
        Assert.Null(_store.Stored);
        Assert.Empty(_vm.Objects);
        Assert.Null(_vm.SelectedObject);
        Assert.Null(_vm.User);
    }
}
