using System.Collections.ObjectModel;
using Flota.App.Services;
using Flota.Client;
using Flota.Core.Contracts;

namespace Flota.App.Presentation.Fleet;

/// <summary>Sortowanie listy obiektów.</summary>
public enum ObjectSort
{
    Status,
    Name,
    Location,
    Visible,
}

/// <summary>Tryb śladu na mapie.</summary>
public enum TraceMode
{
    None,
    /// <summary>„Śledź” — ślad bieżącego przejazdu wybranego obiektu.</summary>
    Tracking,
    /// <summary>Raport trasy — punkty wybranych kursów.</summary>
    Report,
}

/// <summary>
/// Stan floty: obiekty z pozycjami, zaznaczenie, filtr, sortowanie, ślad, obszary, widok początkowy mapy.
/// Źródło danych dla listy obiektów, szczegółów i mapy.
/// </summary>
public sealed partial class FleetViewModel : ObservableObject
{
    readonly FlotaApiClient _api;
    readonly FleetHubClient _hub;
    readonly SnapshotCache _cache;
    readonly Notifier _notifier;
    readonly Dictionary<int, ObjectItem> _byId = [];
    DispatcherQueue? _dispatcher;
    DispatcherQueueTimer? _statusTimer;

    public FleetViewModel(FlotaApiClient api, FleetHubClient hub, SnapshotCache cache, Notifier notifier)
    {
        _api = api;
        _hub = hub;
        _cache = cache;
        _notifier = notifier;
        _hub.PositionsChanged += (_, p) => Dispatch(() => ApplyPositions(p));
        _hub.AddressResolved += (_, a) => Dispatch(() => ApplyAddress(a));
        _hub.StateChanged += (_, s) => Dispatch(() => LiveState = s);
    }

    /// <summary>Wszystkie obiekty.</summary>
    public IReadOnlyList<ObjectItem> AllObjects => _byId.Values.ToList();

    /// <summary>Obiekty po filtrze i sortowaniu (lista „Obiekty”).</summary>
    public ObservableCollection<ObjectItem> Objects { get; } = [];

    public IReadOnlyList<AreaInfo> Areas { get; private set; } = [];

    public IReadOnlyList<FleetObject> FleetObjects => _byId.Values.Select(o => o.Source).ToList();

    [ObservableProperty]
    public partial UserInfo? User { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>Wyświetlany stan pochodzi z pamięci lokalnej (przed odświeżeniem).</summary>
    [ObservableProperty]
    public partial bool IsStale { get; set; }

    [ObservableProperty]
    public partial LiveConnectionState LiveState { get; set; }

    [ObservableProperty]
    public partial ObjectItem? SelectedObject { get; set; }

    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsTracking { get; set; }

    [ObservableProperty]
    public partial bool ShowAreas { get; set; }

    public ObjectSort Sort { get; private set; } = ObjectSort.Status;
    public bool SortDescending { get; private set; }

    public UserSettings Settings { get; private set; } = new(null, null, null);

    /// <summary>Ślad na mapie i jego tryb.</summary>
    public IReadOnlyList<ObjectPosition> Trace { get; private set; } = [];
    public TraceMode TraceMode { get; private set; }
    /// <summary>Indeks punktu bieżącego śladu raportu (znacznik obiektu).</summary>
    public int TraceCurrentIndex { get; private set; } = -1;

    /// <summary>Obiekty przerysowane w całości (wczytanie, filtr widoczności).</summary>
    public event EventHandler? ObjectsReset;
    /// <summary>Zmienione pozycje obiektów.</summary>
    public event EventHandler<IReadOnlyList<ObjectItem>>? ObjectsUpdated;
    public event EventHandler? TraceChanged;
    /// <summary>Żądanie wyśrodkowania mapy (szerokość, długość, powiększenie lub <c>null</c>).</summary>
    public event EventHandler<(double Lat, double Lon, double? Zoom)>? CenterRequested;
    public event EventHandler? AreasChanged;

    public void AttachDispatcher(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        if (_statusTimer is not null) return;
        // Status „Nieaktualny” zależy od upływu czasu — przeliczenie co minutę
        _statusTimer = dispatcher.CreateTimer();
        _statusTimer.Interval = TimeSpan.FromMinutes(1);
        _statusTimer.Tick += (_, _) =>
        {
            foreach (var o in _byId.Values) o.RefreshStatus();
            if (Sort == ObjectSort.Status) ApplyView();
            ObjectsUpdated?.Invoke(this, _byId.Values.ToList());
        };
        _statusTimer.Start();
    }

    /// <summary>Start: stan z pamięci lokalnej, następnie odświeżenie z Flota.Api i połączenie z hubem.</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (_byId.Count == 0 && await _cache.LoadAsync() is { } cached)
        {
            ApplySnapshot(cached);
            IsStale = true;
        }
        await RefreshAsync(ct);
        _ = LoadSettingsAsync(ct);
        await _hub.StartAsync(ct);
    }

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            var snapshot = await _api.GetSnapshotAsync(ct);
            ApplySnapshot(snapshot);
            IsStale = false;
            await _cache.SaveAsync(snapshot);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not SessionExpiredException)
        {
            _notifier.Error(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    async Task LoadSettingsAsync(CancellationToken ct)
    {
        try
        {
            Settings = await _api.GetSettingsAsync(ct);
            if (SelectedObject is null) GoToDefaultView();
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not SessionExpiredException)
        {
            // Ustawienia opcjonalne — widok domyślny
        }
    }

    void ApplySnapshot(FleetSnapshot snapshot)
    {
        User = snapshot.User;
        var ids = snapshot.Objects.Select(o => o.Id).ToHashSet();
        foreach (var id in _byId.Keys.Where(k => !ids.Contains(k)).ToList()) _byId.Remove(id);
        foreach (var o in snapshot.Objects)
        {
            if (_byId.TryGetValue(o.Id, out var item)) item.Replace(o);
            else _byId[o.Id] = new ObjectItem(o);
        }
        Areas = snapshot.Areas;
        if (SelectedObject is not null && !_byId.ContainsKey(SelectedObject.Id)) SelectedObject = null;
        ApplyView();
        ObjectsReset?.Invoke(this, EventArgs.Empty);
        AreasChanged?.Invoke(this, EventArgs.Empty);
    }

    void ApplyPositions(IReadOnlyList<PositionChanged> changes)
    {
        var updated = new List<ObjectItem>();
        foreach (var c in changes)
        {
            if (!_byId.TryGetValue(c.ObjectId, out var item)) continue;
            item.ApplyPosition(c.Position);
            updated.Add(item);
        }
        if (updated.Count == 0) return;
        if (Sort == ObjectSort.Status || Sort == ObjectSort.Location) ApplyView();
        ObjectsUpdated?.Invoke(this, updated);
        if (IsTracking && SelectedObject is { } sel && updated.Contains(sel)) _ = LoadTraceAsync();
    }

    void ApplyAddress(AddressResolved a)
    {
        if (!_byId.TryGetValue(a.ObjectId, out var item)) return;
        item.ApplyAddress(a.Time, a.Address);
        ObjectsUpdated?.Invoke(this, [item]);
    }

    /// <summary>Sortowanie listy; ponowny wybór tej samej kolumny odwraca kierunek.</summary>
    public void SetSort(ObjectSort sort)
    {
        SortDescending = Sort == sort && !SortDescending;
        Sort = sort;
        ApplyView();
    }

    partial void OnFilterTextChanged(string value) => ApplyView();

    /// <summary>Filtr i sortowanie listy (odpowiednik <c>SortAndFilterObjects</c>).</summary>
    public void ApplyView()
    {
        IEnumerable<ObjectItem> items = _byId.Values;
        var f = FilterText?.Trim();
        if (!string.IsNullOrEmpty(f))
            items = items.Where(o => o.Name.Contains(f, StringComparison.CurrentCultureIgnoreCase)
                                     || o.RegistrationNumber.Contains(f, StringComparison.CurrentCultureIgnoreCase)
                                     || o.LocationText.Contains(f, StringComparison.CurrentCultureIgnoreCase));
        var cmp = StringComparer.Create(Loc.Pl, ignoreCase: true);
        IOrderedEnumerable<ObjectItem> sorted = Sort switch
        {
            ObjectSort.Name => SortDescending ? items.OrderByDescending(o => o.Name, cmp) : items.OrderBy(o => o.Name, cmp),
            ObjectSort.Location => SortDescending ? items.OrderByDescending(o => o.LocationText, cmp) : items.OrderBy(o => o.LocationText, cmp),
            ObjectSort.Visible => SortDescending ? items.OrderBy(o => o.IsVisibleOnMap) : items.OrderByDescending(o => o.IsVisibleOnMap),
            _ => SortDescending ? items.OrderByDescending(o => o.Status) : items.OrderBy(o => o.Status),
        };
        var list = sorted.ThenBy(o => o.Name, cmp).ToList();

        // Minimalne zmiany kolekcji — zachowanie zaznaczenia i przewinięcia listy
        for (var i = Objects.Count - 1; i >= 0; i--)
            if (!list.Contains(Objects[i])) Objects.RemoveAt(i);
        for (var i = 0; i < list.Count; i++)
        {
            var at = Objects.IndexOf(list[i]);
            if (at == i) continue;
            if (at >= 0) Objects.Move(at, i);
            else Objects.Insert(i, list[i]);
        }
    }

    /// <summary>Wybór obiektu (lista, mapa, raport): zaznaczenie, wyśrodkowanie mapy, ślad w trybie „Śledź”.</summary>
    public void Select(ObjectItem? item, bool center = true)
    {
        if (SelectedObject is { } prev && prev != item) prev.IsSelected = false;
        SelectedObject = item;
        if (item is null) return;
        item.IsSelected = true;
        if (!item.IsVisibleOnMap && item.Enabled) item.IsVisibleOnMap = true;
        ObjectsUpdated?.Invoke(this, [item]);
        if (IsTracking) _ = LoadTraceAsync();
        else if (center && item.Position is { HasLocation: true } p) CenterRequested?.Invoke(this, (p.Latitude, p.Longitude, null));
    }

    public ObjectItem? Find(int id) => _byId.GetValueOrDefault(id);

    /// <summary>Przybliżenie do obiektu (dwuklik na liście).</summary>
    public void ZoomTo(ObjectItem item)
    {
        if (item.Position is { HasLocation: true } p) CenterRequested?.Invoke(this, (p.Latitude, p.Longitude, 18));
    }

    public void SetAllVisible(bool visible)
    {
        foreach (var o in _byId.Values) o.IsVisibleOnMap = visible && o.Enabled;
        ObjectsReset?.Invoke(this, EventArgs.Empty);
    }

    public void OnVisibilityChanged(ObjectItem item) => ObjectsUpdated?.Invoke(this, [item]);

    [RelayCommand]
    public async Task ToggleTrackingAsync()
    {
        IsTracking = !IsTracking;
        if (IsTracking) await LoadTraceAsync();
        else SetTrace([], TraceMode.None);
    }

    /// <summary>Ślad bieżącego przejazdu: wiadomości od ostatniej pozycji − 5 min (odpowiednik <c>DrawTrace</c>).</summary>
    async Task LoadTraceAsync()
    {
        if (SelectedObject is not { Position: { } p } item) return;
        try
        {
            var trace = await _api.GetTraceAsync(item.Id, p.Time.AddMinutes(-5), DateTime.Now, default);
            if (IsTracking && SelectedObject == item) SetTrace(trace, TraceMode.Tracking);
        }
        catch (Exception ex) when (ex is not SessionExpiredException)
        {
            _notifier.Error(ex);
        }
    }

    /// <summary>Ślad raportu trasy (punkty wybranych kursów) i punkt bieżący.</summary>
    public void SetReportTrace(IReadOnlyList<ObjectPosition> points, int currentIndex)
    {
        if (points.Count == 0)
        {
            if (TraceMode == TraceMode.Report) SetTrace([], TraceMode.None);
            return;
        }
        IsTracking = false;
        TraceCurrentIndex = currentIndex;
        SetTrace(points, TraceMode.Report);
    }

    void SetTrace(IReadOnlyList<ObjectPosition> points, TraceMode mode)
    {
        Trace = points;
        TraceMode = points.Count == 0 ? TraceMode.None : mode;
        if (mode != TraceMode.Report) TraceCurrentIndex = points.Count - 1;
        TraceChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnShowAreasChanged(bool value) => AreasChanged?.Invoke(this, EventArgs.Empty);

    public void CenterOnArea(AreaInfo area)
    {
        if (area.Outline.Count == 0) return;
        CenterRequested?.Invoke(this, (area.Outline.Average(p => p.Latitude), area.Outline.Average(p => p.Longitude), null));
    }

    /// <summary>Widok początkowy mapy („Przejdź”).</summary>
    public void GoToDefaultView()
    {
        if (Settings is { DefaultLatitude: { } lat, DefaultLongitude: { } lon })
            CenterRequested?.Invoke(this, (lat, lon, Settings.DefaultZoom));
    }

    /// <summary>„Ustaw bieżący” — zapis środka i powiększenia mapy.</summary>
    public async Task SaveDefaultViewAsync(double lat, double lon, double zoom)
    {
        try
        {
            Settings = new UserSettings(lat, lon, zoom);
            await _api.SaveSettingsAsync(Settings, default);
            _notifier.Success(Loc.Get("DefaultViewSaved"));
        }
        catch (Exception ex) when (ex is not SessionExpiredException)
        {
            _notifier.Error(ex);
        }
    }

    /// <summary>„Usuń” — usunięcie widoku początkowego.</summary>
    public async Task DeleteDefaultViewAsync()
    {
        try
        {
            Settings = new UserSettings(null, null, null);
            await _api.SaveSettingsAsync(Settings, default);
            _notifier.Success(Loc.Get("DefaultViewDeleted"));
        }
        catch (Exception ex) when (ex is not SessionExpiredException)
        {
            _notifier.Error(ex);
        }
    }

    public async Task ClearAsync()
    {
        await _hub.StopAsync();
        await _cache.ClearAsync();
        _byId.Clear();
        Objects.Clear();
        SelectedObject = null;
        User = null;
        SetTrace([], TraceMode.None);
        ObjectsReset?.Invoke(this, EventArgs.Empty);
    }

    void Dispatch(Action action)
    {
        if (_dispatcher is null || _dispatcher.HasThreadAccess) action();
        else _dispatcher.TryEnqueue(() => action());
    }
}
