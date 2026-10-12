using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Flota.Client;
using Flota.Core.Contracts;
using Flota.Core.Periods;
using Flota.Presentation.Fleet;

namespace Flota.Presentation.Reports;

/// <summary>Zakładki raportu trasy.</summary>
public enum RouteTab
{
    Route,
    Stages,
    Waypoints,
    Fuel,
    Events,
    DrivingStyle,
    StageSummary,
}

/// <summary>
/// Raport trasy (odpowiednik <c>Components/Reports.razor</c>): parametry, kryteria postoju i stylu jazdy, zakładki ładowane leniwie,
/// odtwarzanie trasy, ślad na mapie.
/// </summary>
public sealed partial class RouteReportViewModel : ObservableObject, IDisposable
{
    /// <summary>Liczba punktów, powyżej której ślad nie jest rysowany (jak w aplikacji źródłowej).</summary>
    public const int MaxDrawnPoints = 5000;
    static readonly TimeSpan PlaybackStep = TimeSpan.FromSeconds(3);

    readonly FlotaApiClient _api;
    readonly FleetViewModel _fleet;
    readonly Notifier _notifier;
    readonly TimeProvider _time;
    readonly HashSet<RouteTab> _loaded = [];
    CancellationTokenSource? _playback;
    RouteQuery? _query;

    public RouteReportViewModel(FlotaApiClient api, FleetViewModel fleet, Notifier notifier, TimeProvider time)
    {
        _api = api;
        _fleet = fleet;
        _notifier = notifier;
        _time = time;
        ApplyPeriod();
        // Model tworzony razem z panelem (przy pierwszym otwarciu) — obiekt zaznaczony wcześniej
        if (_fleet.SelectedObject is { } selected) ObjectId = selected.Id;
        _fleet.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FleetViewModel.SelectedObject) && _fleet.SelectedObject is { } o && o.Id != ObjectId)
                ObjectId = o.Id;
        };
    }

    // ---------------- Parametry ----------------

    [ObservableProperty] public partial PeriodType Period { get; set; } = PeriodType.LastHour;
    [ObservableProperty] public partial DateTime From { get; set; }
    [ObservableProperty] public partial DateTime To { get; set; }
    [ObservableProperty] public partial int ObjectId { get; set; }
    [ObservableProperty] public partial bool StageSubtypeBySensor { get; set; } = true;
    [ObservableProperty] public partial bool IgnitionOffEnabled { get; set; } = true;
    [ObservableProperty] public partial string IgnitionOffTime { get; set; } = "00:01:00";
    [ObservableProperty] public partial bool StopEnabled { get; set; }
    [ObservableProperty] public partial string StopTime { get; set; } = "00:02:00";
    [ObservableProperty] public partial double MinSpeed { get; set; } = 5;
    [ObservableProperty] public partial double Speeding { get; set; } = 50;
    [ObservableProperty] public partial double ExceedRpm { get; set; } = 2000;
    [ObservableProperty] public partial double SuddenBraking { get; set; } = 1;
    [ObservableProperty] public partial double QuickAcceleration { get; set; } = 1;

    public bool IsCustomPeriod => Period == PeriodType.NonStandard;

    partial void OnPeriodChanged(PeriodType value)
    {
        ApplyPeriod();
        OnPropertyChanged(nameof(IsCustomPeriod));
    }

    partial void OnObjectIdChanged(int value)
    {
        Generated = false;
        Stop();
    }

    /// <summary>Kryteria postoju: co najmniej jedno włączone (odpowiednik <c>CheckedChanged</c>).</summary>
    partial void OnIgnitionOffEnabledChanged(bool value)
    {
        if (!value && !StopEnabled) StopEnabled = true;
    }

    partial void OnStopEnabledChanged(bool value)
    {
        if (!value && !IgnitionOffEnabled) IgnitionOffEnabled = true;
    }

    void ApplyPeriod() => (From, To) = PeriodPreset.Compute(Period, _time.GetLocalNow().DateTime, (From, To));

    // ---------------- Stan ----------------

    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool Generated { get; set; }
    [ObservableProperty] public partial RouteTab SelectedTab { get; set; }
    [ObservableProperty] public partial bool IsPlaying { get; set; }

    public RouteReport? Report { get; private set; }
    public IReadOnlyList<RouteStage> SelectedStages { get; private set; } = [];
    public WaypointsResponse? Waypoints { get; private set; }
    public int CurrentWaypoint { get; private set; } = -1;
    public FuelReport? Fuel { get; private set; }
    public IReadOnlyList<ObjectEvent> Events { get; private set; } = [];
    public DrivingStyle? Style { get; private set; }
    public IReadOnlyList<RouteStage> StageSummary { get; private set; } = [];

    public bool HasFuel => Report?.HasFuelData == true;

    public string ObjectName => Report?.ObjectName ?? _fleet.Find(ObjectId)?.Name ?? string.Empty;

    /// <summary>Wynik zmieniony — przebudowa widoku zakładki.</summary>
    public event EventHandler<RouteTab>? TabDataChanged;
    /// <summary>Zmiana bieżącego punktu trasy.</summary>
    public event EventHandler? CurrentWaypointChanged;

    RouteQuery BuildQuery() => new()
    {
        From = From,
        To = To,
        StopTime = StopEnabled ? ParseTime(StopTime, TimeSpan.FromMinutes(2)) : null,
        IgnitionOffTime = IgnitionOffEnabled ? ParseTime(IgnitionOffTime, TimeSpan.FromMinutes(1)) : null,
        MinSpeed = StopEnabled ? (decimal)Math.Max(0, MinSpeed) : 0,
        StageSubtypeBySensor = StageSubtypeBySensor,
    };

    static TimeSpan ParseTime(string value, TimeSpan fallback) =>
        TimeSpan.TryParseExact(value?.Trim(), [@"hh\:mm\:ss", @"h\:mm\:ss", @"hh\:mm"], CultureInfo.InvariantCulture, out var t) ? t : fallback;

    /// <summary>„Generuj raport” / „Generuj raport (trasa)” (odpowiednik <c>ReportsClick</c>).</summary>
    public async Task GenerateAsync(bool showRoute)
    {
        if (ObjectId <= 0)
        {
            _notifier.Info(Loc.Get("PickObjectFirst"));
            return;
        }
        Stop();
        if (Period != PeriodType.NonStandard) ApplyPeriod();
        IsBusy = true;
        _loaded.Clear();
        Report = null;
        Waypoints = null;
        Fuel = null;
        Events = [];
        Style = null;
        StageSummary = [];
        SelectedStages = [];
        CurrentWaypoint = -1;
        _fleet.SetReportTrace([], -1);
        try
        {
            _query = BuildQuery();
            Report = await _api.GetRouteAsync(ObjectId, _query, CancellationToken.None);
            _loaded.Add(RouteTab.Route);
            _loaded.Add(RouteTab.Stages);
            Generated = true;
            OnPropertyChanged(nameof(HasFuel));
            OnPropertyChanged(nameof(ObjectName));
            TabDataChanged?.Invoke(this, RouteTab.Route);
            TabDataChanged?.Invoke(this, RouteTab.Stages);
            if (showRoute && Report is { Stages.Count: > 0 })
            {
                SelectedStages = Report.Stages;
                TabDataChanged?.Invoke(this, RouteTab.Stages);
                SelectedTab = RouteTab.Waypoints;
                await LoadTabAsync(RouteTab.Waypoints);
            }
            else
            {
                SelectedTab = RouteTab.Route;
            }
        }
        catch (Exception ex) when (ex is not SessionExpiredException)
        {
            _notifier.Error(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Wczytanie zakładki przy pierwszym otwarciu (odpowiednik <c>OnTabChange</c>).</summary>
    public async Task LoadTabAsync(RouteTab tab)
    {
        Stop();
        if (Report is null || _query is null || _loaded.Contains(tab)) return;
        IsBusy = true;
        try
        {
            switch (tab)
            {
                case RouteTab.Waypoints:
                    await LoadWaypointsAsync();
                    break;
                case RouteTab.Fuel:
                    Fuel = await _api.GetFuelAsync(ObjectId, _query.From, _query.To, CancellationToken.None);
                    break;
                case RouteTab.Events:
                    Events = await _api.GetEventsAsync(ObjectId, _query.From, _query.To, CancellationToken.None);
                    break;
                case RouteTab.DrivingStyle:
                    Style = await _api.GetDrivingStyleAsync(ObjectId, new DrivingStyleQuery
                    {
                        Route = _query,
                        Speeding = (decimal)Speeding,
                        ExceedRpm = (decimal)ExceedRpm,
                        SuddenBraking = (decimal)SuddenBraking,
                        QuickAcceleration = (decimal)QuickAcceleration,
                    }, CancellationToken.None);
                    break;
                case RouteTab.StageSummary:
                    StageSummary = await _api.GetStageSummaryAsync(ObjectId, _query, CancellationToken.None);
                    break;
            }
            _loaded.Add(tab);
            TabDataChanged?.Invoke(this, tab);
        }
        catch (Exception ex) when (ex is not SessionExpiredException)
        {
            _notifier.Error(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Wybór kursów (zakładka „Kursy”); punkty trasy wczytywane ponownie przy następnym otwarciu.</summary>
    public void SetSelectedStages(IReadOnlyList<RouteStage> stages)
    {
        SelectedStages = stages;
        _loaded.Remove(RouteTab.Waypoints);
        Waypoints = null;
        CurrentWaypoint = -1;
        if (stages.Count == 0) _fleet.SetReportTrace([], -1);
    }

    async Task LoadWaypointsAsync()
    {
        if (SelectedStages.Count == 0) return;
        var windows = SelectedStages.Select(s => new StageWindow(s.Type, s.BeginDate, s.EndDate)).ToList();
        Waypoints = await _api.GetWaypointsAsync(ObjectId, windows, CancellationToken.None);
        if (Waypoints.Points.Count > MaxDrawnPoints)
            _notifier.Info(Loc.Format("TooManyPoints", Waypoints.Points.Count));
        SetCurrent(Waypoints.Points.Count > 0 ? 0 : -1);
    }

    /// <summary>Punkt bieżący trasy: znacznik obiektu i przesunięcie mapy.</summary>
    public void SetCurrent(int index)
    {
        if (Waypoints is null) return;
        CurrentWaypoint = Math.Clamp(index, -1, Waypoints.Points.Count - 1);
        var points = Waypoints.Points;
        if (points.Count > MaxDrawnPoints)
        {
            // Ślad niewyświetlany — tylko pozycja bieżąca
            _fleet.SetReportTrace(CurrentWaypoint >= 0 ? [points[CurrentWaypoint].Position] : [], 0);
        }
        else
        {
            _fleet.SetReportTrace(points.Select(p => p.Position).ToList(), CurrentWaypoint);
        }
        if (_fleet.Find(ObjectId) is { } o && _fleet.SelectedObject != o) _fleet.Select(o, center: false);
        CurrentWaypointChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    public bool CanGoBack => Waypoints is { Points.Count: > 0 } && CurrentWaypoint > 0 && !IsPlaying;
    public bool CanGoForward => Waypoints is { Points.Count: > 0 } && CurrentWaypoint < Waypoints.Points.Count - 1 && !IsPlaying;

    public void First() => SetCurrent(0);
    public void Back() => SetCurrent(CurrentWaypoint - 1);
    public void Forward() => SetCurrent(CurrentWaypoint + 1);
    public void Last() => SetCurrent((Waypoints?.Points.Count ?? 0) - 1);

    /// <summary>„Start”: kolejne punkty co 3 s od bieżącego.</summary>
    public async Task PlayAsync()
    {
        if (Waypoints is not { Points.Count: > 0 } || IsPlaying) return;
        _playback?.Dispose();
        _playback = new CancellationTokenSource();
        var ct = _playback.Token;
        IsPlaying = true;
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        try
        {
            for (var i = Math.Max(0, CurrentWaypoint); i < Waypoints.Points.Count && !ct.IsCancellationRequested; i++)
            {
                SetCurrent(i);
                await Task.Delay(PlaybackStep, _time, ct);
            }
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            IsPlaying = false;
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
        }
    }

    public void Stop() => _playback?.Cancel();

    public void Dispose() => _playback?.Dispose();

    public void Reset()
    {
        Stop();
        Report = null;
        Waypoints = null;
        Generated = false;
        _loaded.Clear();
    }

    public DateTime? QueryFrom => _query?.From;
    public DateTime? QueryTo => _query?.To;
}
