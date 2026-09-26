using System.ComponentModel;
using Flota.App.Controls;
using Flota.App.Presentation.Fleet;
using Flota.App.Presentation.Reports;
using Flota.App.Services;
using Flota.Core.Contracts;
using Flota.Core.Periods;
using Flota.Core.Reports;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;

namespace Flota.App.Presentation.Panels;

/// <summary>Panel „Raport trasy” (odpowiednik <c>Components/Reports.razor</c>).</summary>
public sealed partial class RouteReportPanel : UserControl
{
    readonly RouteReportViewModel _vm;
    readonly FleetViewModel _fleet;
    readonly ExportService _export;
    bool _syncing;

    public RouteReportPanel()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<RouteReportViewModel>();
        _fleet = App.Services.GetRequiredService<FleetViewModel>();
        _export = App.Services.GetRequiredService<ExportService>();

        PeriodBox.ItemsSource = PeriodPreset.All.Select(PeriodPreset.DisplayName).ToList();
        _vm.PropertyChanged += OnVmChanged;
        _vm.TabDataChanged += (_, tab) => RenderTab(tab);
        _vm.CurrentWaypointChanged += (_, _) => SyncCurrentWaypoint();
        _fleet.ObjectsReset += (_, _) => FillObjects();
        StagesTable.SelectionChanged += (_, items) => _vm.SetSelectedStages(items.Cast<RouteStage>().OrderBy(s => s.Index).ToList());
        WaypointsTable.SelectionChanged += (_, items) =>
        {
            if (_syncing || items.FirstOrDefault() is not Waypoint w) return;
            _vm.SetCurrent(w.Id);
        };

        FillObjects();
        LoadParameters();
        SetTab(RouteTab.Route);
        UpdateButtons();
    }

    // ---------------- Parametry ----------------

    void LoadParameters()
    {
        _syncing = true;
        PeriodBox.SelectedIndex = (int)_vm.Period;
        SetDates();
        SubtypeBox.IsChecked = _vm.StageSubtypeBySensor;
        IgnitionBox.IsChecked = _vm.IgnitionOffEnabled;
        IgnitionTimeBox.Text = _vm.IgnitionOffTime;
        StopBox.IsChecked = _vm.StopEnabled;
        StopTimeBox.Text = _vm.StopTime;
        MinSpeedBox.Value = _vm.MinSpeed;
        SpeedingBox.Value = _vm.Speeding;
        BrakingBox.Value = _vm.SuddenBraking;
        RpmBox.Value = _vm.ExceedRpm;
        AccelerationBox.Value = _vm.QuickAcceleration;
        UpdateCriteriaEnabled();
        _syncing = false;
    }

    void SetDates()
    {
        var was = _syncing;
        _syncing = true;
        FromDate.Date = _vm.From.Date;
        FromTime.Time = _vm.From.TimeOfDay;
        ToDate.Date = _vm.To.Date;
        ToTime.Time = _vm.To.TimeOfDay;
        var custom = _vm.IsCustomPeriod;
        FromDate.IsEnabled = FromTime.IsEnabled = ToDate.IsEnabled = ToTime.IsEnabled = custom;
        _syncing = was;
    }

    void FillObjects()
    {
        var items = _fleet.FleetObjects.Where(o => o.IsActive).OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(o => new ObjectsPicker.PickerItem(o.Id, string.IsNullOrEmpty(o.RegistrationNumber) ? o.Name : $"{o.Name} ({o.RegistrationNumber})")).ToList();
        _syncing = true;
        ObjectBox.ItemsSource = items;
        ObjectBox.SelectedItem = items.FirstOrDefault(i => i.Id == _vm.ObjectId);
        _syncing = false;
    }

    void OnPeriodChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || PeriodBox.SelectedIndex < 0) return;
        _vm.Period = (PeriodType)PeriodBox.SelectedIndex;
    }

    void OnDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args) => ReadDates();

    void OnTimeChanged(object? sender, TimePickerValueChangedEventArgs e) => ReadDates();

    void ReadDates()
    {
        if (_syncing || !_vm.IsCustomPeriod) return;
        if (FromDate.Date is { } fd) _vm.From = fd.Date + FromTime.Time;
        if (ToDate.Date is { } td) _vm.To = td.Date + ToTime.Time;
    }

    void OnObjectChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || ObjectBox.SelectedItem is not ObjectsPicker.PickerItem item) return;
        _vm.ObjectId = item.Id;
        if (_fleet.Find(item.Id) is { } o) _fleet.Select(o);
    }

    void OnCriteriaChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        _vm.StageSubtypeBySensor = SubtypeBox.IsChecked == true;
        if (sender == IgnitionBox) _vm.IgnitionOffEnabled = IgnitionBox.IsChecked == true;
        if (sender == StopBox) _vm.StopEnabled = StopBox.IsChecked == true;
        _syncing = true;
        IgnitionBox.IsChecked = _vm.IgnitionOffEnabled;
        StopBox.IsChecked = _vm.StopEnabled;
        _syncing = false;
        UpdateCriteriaEnabled();
    }

    void UpdateCriteriaEnabled()
    {
        IgnitionTimeBox.IsEnabled = _vm.IgnitionOffEnabled;
        StopTimeBox.IsEnabled = _vm.StopEnabled;
        MinSpeedBox.IsEnabled = _vm.StopEnabled;
    }

    void OnCriteriaTextChanged(object sender, RoutedEventArgs e)
    {
        _vm.IgnitionOffTime = IgnitionTimeBox.Text;
        _vm.StopTime = StopTimeBox.Text;
    }

    void OnNumberChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_syncing || double.IsNaN(args.NewValue)) return;
        if (sender == MinSpeedBox) _vm.MinSpeed = args.NewValue;
        else if (sender == SpeedingBox) _vm.Speeding = args.NewValue;
        else if (sender == BrakingBox) _vm.SuddenBraking = args.NewValue;
        else if (sender == RpmBox) _vm.ExceedRpm = args.NewValue;
        else if (sender == AccelerationBox) _vm.QuickAcceleration = args.NewValue;
    }

    void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RouteReportViewModel.From):
            case nameof(RouteReportViewModel.To):
            case nameof(RouteReportViewModel.IsCustomPeriod):
                SetDates();
                break;
            case nameof(RouteReportViewModel.ObjectId):
                _syncing = true;
                ObjectBox.SelectedItem = (ObjectBox.ItemsSource as IEnumerable<ObjectsPicker.PickerItem>)?.FirstOrDefault(i => i.Id == _vm.ObjectId);
                _syncing = false;
                if (!_vm.Generated) TabsScroll.Visibility = Visibility.Collapsed;
                UpdateButtons();
                break;
            case nameof(RouteReportViewModel.IsBusy):
                BusyBar.Visibility = _vm.IsBusy ? Visibility.Visible : Visibility.Collapsed;
                UpdateButtons();
                break;
            case nameof(RouteReportViewModel.Generated):
                TabsScroll.Visibility = _vm.Generated ? Visibility.Visible : Visibility.Collapsed;
                if (!_vm.Generated) HideTabs();
                UpdateButtons();
                break;
            case nameof(RouteReportViewModel.SelectedTab):
                SetTab(_vm.SelectedTab);
                break;
            case nameof(RouteReportViewModel.IsPlaying):
            case nameof(RouteReportViewModel.CanGoBack):
            case nameof(RouteReportViewModel.CanGoForward):
                UpdateButtons();
                break;
        }
    }

    void UpdateButtons()
    {
        var hasObject = _vm.ObjectId > 0;
        GenerateButton.IsEnabled = GenerateRouteButton.IsEnabled = hasObject && !_vm.IsBusy;
        var hasPoints = _vm.Waypoints is { Points.Count: > 0 };
        FirstButton.IsEnabled = BackButton.IsEnabled = hasPoints && _vm.CanGoBack;
        ForwardButton.IsEnabled = LastButton.IsEnabled = hasPoints && _vm.CanGoForward;
        PlayButton.IsEnabled = hasPoints && !_vm.IsPlaying;
        StopButton.IsEnabled = _vm.IsPlaying;
        ExcelButton.IsEnabled = _vm.Report is not null && !_vm.IsBusy;
        ExportStages.IsEnabled = _vm.Report is { Stages.Count: > 0 };
        ExportWaypoints.IsEnabled = hasPoints;
        ExportRefuel.IsEnabled = _vm.Fuel is { Refueling.Count: > 0 };
        ExportDefuel.IsEnabled = _vm.Fuel is { Defueling.Count: > 0 };
        ExportEvents.IsEnabled = _vm.Events.Count > 0;
        ExportStageSummary.IsEnabled = _vm.StageSummary.Count > 0;
        TabFuel.Visibility = _vm.HasFuel ? Visibility.Visible : Visibility.Collapsed;
        TabWaypoints.IsEnabled = _vm.SelectedStages.Count > 0 || _vm.Waypoints is not null;
    }

    // ---------------- Polecenia ----------------

    async void OnGenerateClick(object sender, RoutedEventArgs e) => await _vm.GenerateAsync(showRoute: false);

    async void OnGenerateRouteClick(object sender, RoutedEventArgs e) => await _vm.GenerateAsync(showRoute: true);

    void OnFirstClick(object sender, RoutedEventArgs e) => _vm.First();
    void OnBackClick(object sender, RoutedEventArgs e) => _vm.Back();
    async void OnPlayClick(object sender, RoutedEventArgs e) => await _vm.PlayAsync();
    void OnStopClick(object sender, RoutedEventArgs e) => _vm.Stop();
    void OnForwardClick(object sender, RoutedEventArgs e) => _vm.Forward();
    void OnLastClick(object sender, RoutedEventArgs e) => _vm.Last();

    async void OnTabClick(object sender, RoutedEventArgs e)
    {
        var tab = Enum.Parse<RouteTab>((string)((FrameworkElement)sender).Tag);
        _vm.SelectedTab = tab;
        SetTab(tab);
        await _vm.LoadTabAsync(tab);
    }

    void OnSelectAllStages(object sender, RoutedEventArgs e)
    {
        StagesTable.Select(StagesTable.Items);
        _vm.SetSelectedStages(StagesTable.SelectedItems.Cast<RouteStage>().OrderBy(s => s.Index).ToList());
        UpdateButtons();
    }

    async void OnShowStagesOnMap(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedStages.Count == 0)
        {
            App.Services.GetRequiredService<Notifier>().Info(Loc.Get("PickStageFirst"));
            return;
        }
        _vm.SelectedTab = RouteTab.Waypoints;
        SetTab(RouteTab.Waypoints);
        await _vm.LoadTabAsync(RouteTab.Waypoints);
    }

    void SetTab(RouteTab tab)
    {
        foreach (var b in Tabs.Children.OfType<ToolButton>()) b.IsChecked = (string)b.Tag == tab.ToString();
        HideTabs();
        if (!_vm.Generated) return;
        switch (tab)
        {
            case RouteTab.Route: RouteView.Visibility = Visibility.Visible; break;
            case RouteTab.Stages: StagesView.Visibility = Visibility.Visible; break;
            case RouteTab.Waypoints: WaypointsView.Visibility = Visibility.Visible; break;
            case RouteTab.Fuel: FuelView.Visibility = Visibility.Visible; break;
            case RouteTab.Events: EventsTable.Visibility = Visibility.Visible; break;
            case RouteTab.DrivingStyle: StyleView.Visibility = Visibility.Visible; break;
            case RouteTab.StageSummary: SummaryTable.Visibility = Visibility.Visible; break;
        }
        UpdateButtons();
    }

    void HideTabs()
    {
        foreach (var child in TabHost.Children.OfType<UIElement>()) child.Visibility = Visibility.Collapsed;
    }

    // ---------------- Zakładki ----------------

    void RenderTab(RouteTab tab)
    {
        switch (tab)
        {
            case RouteTab.Route: RenderRoute(); break;
            case RouteTab.Stages: RenderStages(); break;
            case RouteTab.Waypoints: RenderWaypoints(); break;
            case RouteTab.Fuel: RenderFuel(); break;
            case RouteTab.Events: EventsTable.SetData(EventColumns, _vm.Events); break;
            case RouteTab.DrivingStyle: RenderStyle(); break;
            case RouteTab.StageSummary: SummaryTable.SetData(SummaryColumns(), _vm.StageSummary); break;
        }
        UpdateButtons();
        SetTab(_vm.SelectedTab);
    }

    void RenderRoute()
    {
        RouteView.Children.Clear();
        if (_vm.Report is not { } r) return;

        var period = Section("RouteSectionPeriod");
        Pairs(period, ("RouteBeginDate", Loc.DateTime(r.BeginDate)), ("RouteEndDate", Loc.DateTime(r.EndDate)));

        var summary = Section("RouteSectionSummary");
        if (r.TotalDistanceCan is { } odo) Pairs(summary, ("RouteOdometerEnd", $"{Loc.N0(odo)} km"));
        Pairs(summary, ("RouteDistance", r.Distance is { } d ? $"{Loc.N1(d)} km" : null), ("RouteDistanceGps", r.DistanceGps is { } dg ? $"{Loc.N1(dg)} km" : null));
        Pairs(summary, ("RouteRideTime", Loc.Duration(r.RideTime)), ("RouteStopTimeValue", Loc.Duration(r.StopTime)));
        if (r.HaulTime is { TotalSeconds: > 0 } ht) Pairs(summary, ("RouteHaulTime", Loc.Duration(ht)));
        Pairs(summary, ("RouteAverageSpeed", r.AverageSpeed is { } av ? $"{Loc.N0(av)} km/h" : null), ("RouteMaxSpeed", r.MaxSpeed is { } mx ? $"{Loc.N1(mx)} km/h" : null));
        if (r.MaxRpm is { } rpm) Pairs(summary, ("RouteMaxRpm", Loc.N0(rpm)));

        if (_vm.HasFuel)
        {
            var fuel = Section("RouteSectionFuel");
            if (r.FuelUsed is { } fu) Pairs(fuel, ("RouteFuelUsed", $"{Loc.N1(fu)} l"));
            var consumption = string.Join("   ", new[]
            {
                r.FuelConsumption is { } c ? $"{Loc.N1(c)} l/100 km" : null,
                r.FuelInTimeConsumption is { } ch ? $"{Loc.N1(ch)} l/godz." : null,
            }.Where(s => s is not null));
            if (consumption.Length > 0) Pairs(fuel, ("RouteConsumption", consumption));
            if (_vm.Fuel is { Fueled: > 0 } f) Pairs(fuel, ("RouteFueled", $"{Loc.N1(f.Fueled)} l"));
        }

        if (r.SensorActivity.Count > 0)
        {
            var sensors = Section("RouteSectionSensors");
            foreach (var s in r.SensorActivity) PairRaw(sensors, s.Name, s.Value);
        }
    }

    Grid Section(string titleKey)
    {
        var header = new TextBlock { Text = Loc.Get(titleKey), Style = (Style)Application.Current.Resources["FlotaHeaderTextStyle"], Margin = new Thickness(0, 8, 0, 2) };
        RouteView.Children.Add(header);
        var g = new Grid { ColumnSpacing = 12, RowSpacing = 4 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        RouteView.Children.Add(g);
        return g;
    }

    static void Pairs(Grid g, params (string Key, string? Value)[] pairs)
    {
        var visible = pairs.Where(p => !string.IsNullOrEmpty(p.Value)).ToList();
        if (visible.Count == 0) return;
        var row = g.RowDefinitions.Count;
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < visible.Count; i++) AddPair(g, row, i * 2, Loc.Get(visible[i].Key), visible[i].Value!);
    }

    static void PairRaw(Grid g, string label, string value)
    {
        var row = g.RowDefinitions.Count;
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddPair(g, row, 0, label, value);
    }

    static void AddPair(Grid g, int row, int col, string label, string value)
    {
        var l = new TextBlock { Text = label, Style = (Style)Application.Current.Resources["FlotaLabelTextStyle"], TextWrapping = TextWrapping.Wrap };
        var v = new TextBlock { Text = value, FontSize = 13, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        Grid.SetRow(l, row);
        Grid.SetColumn(l, col);
        Grid.SetRow(v, row);
        Grid.SetColumn(v, col + 1);
        g.Children.Add(l);
        g.Children.Add(v);
    }

    void RenderStages()
    {
        var stages = _vm.Report?.Stages ?? [];
        StagesTable.SetData(StageColumns(), stages);
        StagesTable.Select(_vm.SelectedStages);
    }

    IReadOnlyList<TableColumn> StageColumns(bool withAddress = false)
    {
        var cols = new List<TableColumn>
        {
            new() { Header = Loc.Get("ColType"), Text = o => ((RouteStage)o).TypeName, Width = 90 },
            new() { Header = Loc.Get("ColBeginDate"), Text = o => Loc.DateTime(((RouteStage)o).BeginDate), SortKey = o => ((RouteStage)o).BeginDate, Width = 150,
                    ExportValue = o => Loc.Raw(((RouteStage)o).BeginDate), ExportKind = ExportColumnKind.DateTime },
            new() { Header = Loc.Get("ColEndDate"), Text = o => Loc.DateTime(((RouteStage)o).EndDate), SortKey = o => ((RouteStage)o).EndDate, Width = 150,
                    ExportValue = o => Loc.Raw(((RouteStage)o).EndDate), ExportKind = ExportColumnKind.DateTime },
            new() { Header = Loc.Get("ColDurationTime"), Text = o => ((RouteStage)o).Time, Width = 100 },
        };
        if (withAddress)
        {
            cols.Add(new() { Header = Loc.Get("ColCity"), Text = o => ((RouteStage)o).Address?.CityOrProvince, Width = 140 });
            cols.Add(new() { Header = Loc.Get("ColStreet"), Text = o => ((RouteStage)o).Address?.StreetOrDistrict, Width = 160 });
        }
        cols.Add(Num("ColDistanceKm", s => s.Distance, Loc.N1));
        cols.Add(Num("ColAverageSpeed", s => s.AverageSpeed, Loc.N0));
        cols.Add(Num("ColMaxSpeed", s => s.MaxSpeed, Loc.N0));
        if (!withAddress)
        {
            cols.Add(Num("ColMaxRpm", s => s.MaxRpm, Loc.N0));
            var report = _vm.Report;
            for (var i = 0; i < (report?.StageColumns.Count ?? 0); i++)
            {
                var key = report!.StageColumns[i].Name;
                cols.Add(new() { Header = report.StageColumns[i].Value, Text = o => ((RouteStage)o).Values.FirstOrDefault(v => v.Name == key)?.Value, Width = 110, Alignment = TextAlignment.Right });
            }
        }
        return cols;

        static TableColumn Num(string header, Func<RouteStage, double?> v, Func<double?, string> fmt) => new()
        {
            Header = Loc.Get(header), Text = o => fmt(v((RouteStage)o)), SortKey = o => v((RouteStage)o), Width = 110, Alignment = TextAlignment.Right,
            ExportValue = o => Loc.Raw(v((RouteStage)o)), ExportKind = ExportColumnKind.Number,
        };
    }

    IReadOnlyList<TableColumn> SummaryColumns() => StageColumns(withAddress: true);

    void RenderWaypoints()
    {
        var w = _vm.Waypoints;
        WaypointsEmpty.Visibility = w is null ? Visibility.Visible : Visibility.Collapsed;
        WaypointsTable.Visibility = w is null ? Visibility.Collapsed : Visibility.Visible;
        if (w is null) return;
        var cols = new List<TableColumn>
        {
            new() { Header = Loc.Get("ColDate"), Text = o => Loc.DateTime(((Waypoint)o).Position.Time), SortKey = o => ((Waypoint)o).Position.Time, Width = 150,
                    ExportValue = o => Loc.Raw(((Waypoint)o).Position.Time), ExportKind = ExportColumnKind.DateTime },
            new() { Header = Loc.Get("ColSpeedKmh"), Text = o => Loc.N0(((Waypoint)o).Position.Speed ?? 0), SortKey = o => ((Waypoint)o).Position.Speed, Width = 100,
                    Alignment = TextAlignment.Right, ExportValue = o => Loc.Raw(Math.Round(((Waypoint)o).Position.Speed ?? 0)), ExportKind = ExportColumnKind.WholeNumber },
            new() { Header = Loc.Get("ColRpm"), Text = o => Loc.N0(((Waypoint)o).Position.Rpm), SortKey = o => ((Waypoint)o).Position.Rpm, Width = 80,
                    Alignment = TextAlignment.Right, ExportValue = o => Loc.Raw(((Waypoint)o).Position.Rpm), ExportKind = ExportColumnKind.WholeNumber },
            new() { Header = Loc.Get("ColOdometerKm"), Text = o => Loc.N0(((Waypoint)o).Position.TotalDistanceCan), SortKey = o => ((Waypoint)o).Position.TotalDistanceCan, Width = 120,
                    Alignment = TextAlignment.Right, ExportValue = o => Loc.Raw(((Waypoint)o).Position.TotalDistanceCan), ExportKind = ExportColumnKind.Number },
            new() { Header = Loc.Get("ColCity"), Text = o => ((Waypoint)o).Position.Address?.CityOrProvince, Width = 140 },
            new() { Header = Loc.Get("ColStreet"), Text = o => ((Waypoint)o).Position.Address?.StreetOrDistrict, Width = 160 },
            new() { Header = Loc.Get("ColIgnition"), Text = o => ((Waypoint)o).Position.IsEngineStarted == true ? "✓" : string.Empty, Width = 70, Alignment = TextAlignment.Center,
                    ExportValue = o => ((Waypoint)o).Position.IsEngineStarted is { } b ? (b ? "true" : "false") : null, ExportKind = ExportColumnKind.Boolean },
        };
        for (var i = 0; i < w.SensorColumns.Count; i++)
        {
            var idx = i;
            cols.Add(new()
            {
                Header = w.SensorColumns[i],
                Text = o => ((Waypoint)o).SensorValues.ElementAtOrDefault(idx) is { } s ? (s.BoolValue is { } b ? (b ? "✓" : string.Empty) : s.Value) : null,
                ExportValue = o => ((Waypoint)o).SensorValues.ElementAtOrDefault(idx) is { } s ? (s.BoolValue is { } b ? (b ? "PRAWDA" : "FAŁSZ") : s.Value) : null,
                Width = 110,
            });
        }
        WaypointsTable.SetData(cols, w.Points);
        SyncCurrentWaypoint();
    }

    void SyncCurrentWaypoint()
    {
        if (_vm.Waypoints is not { } w || _vm.CurrentWaypoint < 0 || _vm.CurrentWaypoint >= w.Points.Count) return;
        _syncing = true;
        WaypointsTable.Select([w.Points[_vm.CurrentWaypoint]]);
        _syncing = false;
        UpdateButtons();
    }

    // ---------------- Paliwo ----------------

    static readonly IReadOnlyList<TableColumn> FuelEventColumns =
    [
        new() { Header = Loc.Get("ColDate"), Text = o => Loc.DateTime(((FuelEvent)o).Date), SortKey = o => ((FuelEvent)o).Date, Width = 150,
                ExportValue = o => Loc.Raw(((FuelEvent)o).Date), ExportKind = ExportColumnKind.DateTime },
        new() { Header = Loc.Get("ColFuelAmount"), Text = o => Loc.N1(((FuelEvent)o).Fuel), SortKey = o => ((FuelEvent)o).Fuel, Width = 110, Alignment = TextAlignment.Right,
                ExportValue = o => Loc.Raw(((FuelEvent)o).Fuel), ExportKind = ExportColumnKind.Number },
        new() { Header = Loc.Get("ColCity"), Text = o => ((FuelEvent)o).Address?.CityOrProvince, Width = 150 },
        new() { Header = Loc.Get("ColStreet"), Text = o => ((FuelEvent)o).Address?.StreetOrDistrict, Width = 170 },
    ];

    void RenderFuel()
    {
        FuelSummary.Children.Clear();
        FuelSummary.RowDefinitions.Clear();
        FuelSummary.ColumnDefinitions.Clear();
        for (var i = 0; i < 4; i++) FuelSummary.ColumnDefinitions.Add(new ColumnDefinition { Width = i % 2 == 0 ? new GridLength(170) : new GridLength(1, GridUnitType.Star) });
        var f = _vm.Fuel;
        if (f is null)
        {
            PairRaw(FuelSummary, Loc.Get("NoData"), string.Empty);
            return;
        }
        Pairs(FuelSummary, ("FuelStartLevel", f.StartLevel is { } s ? $"{Loc.N1(s)} l" : null), ("FuelFinalLevel", f.FinalLevel is { } e ? $"{Loc.N1(e)} l" : null));
        Pairs(FuelSummary, ("FuelTotalUsed", (_vm.Report?.FuelUsed ?? f.FuelUsed) is { } u ? $"{Loc.N1(u)} l" : null), ("FuelFueled", f.Fueled > 0 ? $"{Loc.N1(f.Fueled)} l" : null));
        var r = _vm.Report;
        Pairs(FuelSummary, ("FuelAverage", string.Join("   ", new[]
        {
            r?.FuelConsumption is { } c ? $"{Loc.N1(c)} l/100 km" : null,
            r?.FuelInTimeConsumption is { } ch ? $"{Loc.N1(ch)} l/godz." : null,
        }.Where(x => x is not null))));

        RefuelTable.SetData(FuelEventColumns, f.Refueling);
        DefuelTable.SetData(FuelEventColumns, f.Defueling);
        RenderFuelCharts(f);
        SetFuelTab(0);
        RenderRoute(); // „Suma tankowań” w podsumowaniu trasy
    }

    LiveChartsCore.SkiaSharpView.WinUI.CartesianChart? _fuelTime, _fuelDistance, _style;

    /// <summary>Wykresy tworzone przy pierwszym użyciu (zakładki Paliwo, Styl jazdy).</summary>
    LiveChartsCore.SkiaSharpView.WinUI.CartesianChart FuelTimeChart => _fuelTime ??= NewChart(FuelTimeHost);
    LiveChartsCore.SkiaSharpView.WinUI.CartesianChart FuelDistanceChart => _fuelDistance ??= NewChart(FuelDistanceHost);
    LiveChartsCore.SkiaSharpView.WinUI.CartesianChart StyleChart => _style ??= NewChart(StyleHost);

    static LiveChartsCore.SkiaSharpView.WinUI.CartesianChart NewChart(ContentControl host)
    {
        var chart = new LiveChartsCore.SkiaSharpView.WinUI.CartesianChart();
        host.Content = chart;
        return chart;
    }

    void RenderFuelCharts(FuelReport f)
    {
        var capacity = _vm.Report?.TankCapacity;
        var yMax = capacity is { } cap ? cap + 5 : 120;
        var sections = capacity is { } c
            ? new[] { new RectangularSection { Yi = c, Yj = c, Stroke = new SolidColorPaint(SkColor("ErrorColor"), 2) { PathEffect = new DashEffect([6, 6]) }, Label = Loc.Get("FuelTankCapacity"), LabelSize = 11, LabelPaint = new SolidColorPaint(SkColor("ErrorColor")) } }
            : Array.Empty<RectangularSection>();

        FuelTimeChart.Series =
        [
            new LineSeries<DateTimePoint>
            {
                Values = f.TimeSeries.Select(p => new DateTimePoint(p.Time, Math.Round(p.Value, 1))).ToList(),
                Name = Loc.Get("FuelChartSeries"),
                GeometrySize = 0,
                LineSmoothness = 0.6,
                Stroke = new SolidColorPaint(SkColor("PrimaryColor"), 2),
                Fill = null,
                YToolTipLabelFormatter = p => $"{p.Coordinate.PrimaryValue.ToString("N1", Loc.Pl)} l — {p.Model?.DateTime.ToString("dddd, dd-MM-yyyy HH:mm", Loc.Pl)}",
            },
        ];
        FuelTimeChart.XAxes = [new DateTimeAxis(TimeSpan.FromHours(1), d => d.ToString("HH:mm", Loc.Pl)) { Name = Loc.Get("FuelAxisDate"), NameTextSize = 12, TextSize = 11 }];
        FuelTimeChart.YAxes = [new Axis { MinLimit = 0, MaxLimit = yMax, MinStep = 20, Name = Loc.Get("FuelAxisAmount"), NameTextSize = 12, TextSize = 11, SeparatorsPaint = Paint("OutlineVariantColor") }];
        FuelTimeChart.Sections = sections;
        FuelTimeChart.LegendPosition = LiveChartsCore.Measure.LegendPosition.Hidden;

        FuelDistanceChart.Series =
        [
            new LineSeries<ObservablePoint>
            {
                Values = f.DistanceSeries.Select(p => new ObservablePoint(p.Kilometers, Math.Round(p.Value, 1))).ToList(),
                GeometrySize = 0,
                LineSmoothness = 0.6,
                Stroke = new SolidColorPaint(SkColor("PrimaryColor"), 2),
                Fill = null,
                YToolTipLabelFormatter = p => $"{p.Coordinate.SecondaryValue.ToString("N1", Loc.Pl)} km — {p.Coordinate.PrimaryValue.ToString("N1", Loc.Pl)} l",
            },
        ];
        FuelDistanceChart.XAxes = [new Axis { MinLimit = 0, MinStep = 5, Name = Loc.Get("FuelAxisDistance"), NameTextSize = 12, TextSize = 11 }];
        FuelDistanceChart.YAxes = [new Axis { MinLimit = 0, MaxLimit = yMax, MinStep = 20, Name = Loc.Get("FuelAxisAmount"), NameTextSize = 12, TextSize = 11, SeparatorsPaint = Paint("OutlineVariantColor") }];
        FuelDistanceChart.Sections = sections;
        FuelDistanceChart.LegendPosition = LiveChartsCore.Measure.LegendPosition.Hidden;
    }

    void OnFuelTabClick(object sender, RoutedEventArgs e) => SetFuelTab(int.Parse((string)((FrameworkElement)sender).Tag, Loc.Pl));

    void SetFuelTab(int i)
    {
        FuelTabRefuel.IsChecked = i == 0;
        FuelTabDefuel.IsChecked = i == 1;
        FuelTabTime.IsChecked = i == 2;
        FuelTabDistance.IsChecked = i == 3;
        RefuelTable.Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed;
        DefuelTable.Visibility = i == 1 ? Visibility.Visible : Visibility.Collapsed;
        FuelTimeHost.Visibility = i == 2 ? Visibility.Visible : Visibility.Collapsed;
        FuelDistanceHost.Visibility = i == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------------- Zdarzenia, styl jazdy ----------------

    static readonly IReadOnlyList<TableColumn> EventColumns =
    [
        new() { Header = Loc.Get("ColDate"), Text = o => Loc.DateTime(((ObjectEvent)o).Date), SortKey = o => ((ObjectEvent)o).Date, Width = 150,
                ExportValue = o => Loc.Raw(((ObjectEvent)o).Date), ExportKind = ExportColumnKind.DateTime },
        new() { Header = Loc.Get("ColDescription"), Text = o => ((ObjectEvent)o).Description, Width = 240, Wrap = true },
        new() { Header = Loc.Get("ColCity"), Text = o => ((ObjectEvent)o).Address?.CityOrProvince, Width = 140 },
        new() { Header = Loc.Get("ColStreet"), Text = o => ((ObjectEvent)o).Address?.StreetOrDistrict, Width = 160 },
    ];

    void RenderStyle()
    {
        StyleSummary.Children.Clear();
        StyleSummary.RowDefinitions.Clear();
        StyleSummary.ColumnDefinitions.Clear();
        StyleSummary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        StyleSummary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var s = _vm.Style;
        if (s is null)
        {
            PairRaw(StyleSummary, Loc.Get("NoData"), string.Empty);
            StyleHost.Visibility = Visibility.Collapsed;
            return;
        }
        if (s.SpeedingTime.TotalSeconds > 0) PairRaw(StyleSummary, Loc.Get("StyleSpeedingTime"), ReportRules.FormatDuration(s.SpeedingTime));
        if (s.ExceedRpmTime.TotalSeconds > 0) PairRaw(StyleSummary, Loc.Get("StyleExceedRpmTime"), ReportRules.FormatDuration(s.ExceedRpmTime));
        if (s.QuickAccelerations is { } qa) PairRaw(StyleSummary, Loc.Get("StyleQuickAccelerations"), qa.ToString(Loc.Pl));
        if (s.SuddenBrakings is { } sb) PairRaw(StyleSummary, Loc.Get("StyleSuddenBrakings"), sb.ToString(Loc.Pl));

        var features = s.Features.Where(f => f.Value is not null).ToList();
        StyleHost.Visibility = features.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        StyleChart.Series =
        [
            new LineSeries<double>
            {
                Values = features.Select(f => f.Value!.Value).ToList(),
                Stroke = new SolidColorPaint(SkColor("ErrorColor"), 2),
                Fill = new SolidColorPaint(SkColor("ErrorColor").WithAlpha(90)),
                GeometryFill = new SolidColorPaint(SkColor("SurfaceColor")),
                GeometryStroke = new SolidColorPaint(SkColor("ErrorColor"), 2),
                GeometrySize = 8,
                LineSmoothness = 0,
                DataLabelsPaint = new SolidColorPaint(SkColor("OnSurfaceColor")),
                DataLabelsSize = 11,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
            },
        ];
        StyleChart.XAxes = [new Axis { Labels = features.Select(f => f.Name).ToList(), TextSize = 11, LabelsRotation = 0 }];
        StyleChart.YAxes = [new Axis { MinLimit = 0, MaxLimit = 100, MinStep = 25, TextSize = 11, SeparatorsPaint = Paint("OutlineVariantColor") }];
        StyleChart.LegendPosition = LiveChartsCore.Measure.LegendPosition.Hidden;
    }

    // ---------------- Eksport ----------------

    async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var kind = (string)((FrameworkElement)sender).Tag;
        var suffix = $"{_vm.ObjectName} {_vm.QueryFrom:yyyyMMdd_HHmm} {_vm.QueryTo:yyyyMMdd_HHmm}.xlsx";
        switch (kind)
        {
            case "route":
                await _export.ExportAsync(RouteSummaryTable(), $"{Loc.Get("FileRouteSummary")} {suffix}");
                break;
            case "stages":
                StagesTable.SetData(StageColumns(), _vm.Report?.Stages ?? []);
                await _export.ExportAsync(StagesTable.ToExport(Loc.Get("SheetStages")), $"{Loc.Get("FileStages")} {suffix}");
                StagesTable.Select(_vm.SelectedStages);
                break;
            case "waypoints":
                await _export.ExportAsync(WaypointsTable.ToExport(Loc.Get("SheetWaypoints")), $"{Loc.Get("FileWaypoints")} {suffix}");
                break;
            case "refuel":
                await _export.ExportAsync(RefuelTable.ToExport(Loc.Get("SheetRefuel")), $"{Loc.Get("FileRefuel")} {suffix}");
                break;
            case "defuel":
                await _export.ExportAsync(DefuelTable.ToExport(Loc.Get("SheetDefuel")), $"{Loc.Get("FileDefuel")} {suffix}");
                break;
            case "events":
                await _export.ExportAsync(EventsTable.ToExport(Loc.Get("SheetEvents")), $"{Loc.Get("FileEvents")} {suffix}");
                break;
            case "summary":
                await _export.ExportAsync(SummaryTable.ToExport(Loc.Get("SheetStages")), $"{Loc.Get("FileStageSummary")} {suffix}");
                break;
        }
    }

    /// <summary>Podsumowanie trasy w układzie „nazwa — wartość” (odpowiednik eksportu „Podsumowanie trasy”).</summary>
    ExportTable RouteSummaryTable()
    {
        var rows = new List<IReadOnlyList<string?>>();
        void Add(string label, string? value) => rows.Add([label, value]);
        var r = _vm.Report;
        var o = _fleet.Find(_vm.ObjectId);
        Add(Loc.Get("ColObject"), o is null ? r?.ObjectName : $"{o.Name} ({o.RegistrationNumber})");
        Add(string.Empty, null);
        Add(Loc.Get("RouteSectionPeriod"), null);
        Add(Loc.Get("RouteBeginDate"), Loc.DateTime(r?.BeginDate));
        Add(Loc.Get("RouteEndDate"), Loc.DateTime(r?.EndDate));
        Add(string.Empty, null);
        Add(Loc.Get("RouteSectionSummary"), null);
        if (r?.TotalDistanceCan is { } odo) Add(Loc.Get("RouteOdometerEnd"), $"{Loc.N0(odo)} km");
        if (r?.Distance is { } d) Add(Loc.Get("RouteDistance"), $"{Loc.N1(d)} km");
        if (r?.DistanceGps is { } dg) Add(Loc.Get("RouteDistanceGps"), $"{Loc.N1(dg)} km");
        if (r is not null) Add(Loc.Get("RouteRideTime"), Loc.Duration(r.RideTime));
        if (r?.StopTime is { } st) Add(Loc.Get("RouteStopTimeValue"), Loc.Duration(st));
        if (r?.HaulTime is { TotalSeconds: > 0 } ht) Add(Loc.Get("RouteHaulTime"), Loc.Duration(ht));
        if (r?.AverageSpeed is { } av) Add(Loc.Get("RouteAverageSpeed"), $"{Loc.N0(av)} km/h");
        if (r?.MaxSpeed is { } mx) Add(Loc.Get("RouteMaxSpeed"), $"{Loc.N1(mx)} km/h");
        if (r?.MaxRpm is { } rpm) Add(Loc.Get("RouteMaxRpm"), Loc.N0(rpm));
        if (_vm.HasFuel)
        {
            Add(string.Empty, null);
            Add(Loc.Get("RouteSectionFuel"), null);
            if (r?.FuelUsed is { } fu) Add(Loc.Get("RouteFuelUsed"), $"{Loc.N1(fu)} l");
            if (r?.FuelConsumption is { } c) Add(Loc.Get("RouteConsumption"), $"{Loc.N1(c)} l/100 km");
            if (r?.FuelInTimeConsumption is { } ch) Add(Loc.Get("RouteConsumption"), $"{Loc.N1(ch)} l/godz.");
        }
        if (r is { SensorActivity.Count: > 0 })
        {
            Add(string.Empty, null);
            Add(Loc.Get("RouteSectionSensors"), null);
            foreach (var s in r.SensorActivity) Add(s.Name, s.Value);
        }
        return new ExportTable(Loc.Get("SheetRoute"), [new ExportColumn(string.Empty), new ExportColumn(string.Empty)], rows);
    }

    // ---------------- Kolory wykresów z palety ----------------

    static SKColor SkColor(string key)
    {
        var c = Application.Current.Resources.TryGetValue(key, out var v) && v is Windows.UI.Color wc ? wc : Microsoft.UI.Colors.Gray;
        return new SKColor(c.R, c.G, c.B, c.A);
    }

    static SolidColorPaint Paint(string key) => new(SkColor(key));
}
