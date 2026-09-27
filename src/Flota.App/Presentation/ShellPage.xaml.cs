using System.ComponentModel;
using Flota.App.Controls;
using Flota.App.Services;
using Flota.Client;

namespace Flota.App.Presentation;

/// <summary>
/// Strona główna (odpowiednik <c>Pages/Index-a.razor</c>): pasek przełączników paneli, trzy kolumny (lewa 3/12, mapa 9/12 lub 4/12,
/// prawa 5/12), panel boczny. Na ekranie wąskim (szerokość &lt; 1168 px lub orientacja pionowa) mapa u góry, panele pod nią.
/// </summary>
public sealed partial class ShellPage : Page
{
    const double CompactWidth = 1168;

    readonly ShellViewModel _vm;
    readonly FleetViewModel _fleet;
    readonly Notifier _notifier;
    bool _initialized;

    public ShellPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<ShellViewModel>();
        _fleet = App.Services.GetRequiredService<FleetViewModel>();
        _notifier = App.Services.GetRequiredService<Notifier>();
        _vm.PropertyChanged += OnShellPropertyChanged;
        _fleet.PropertyChanged += OnFleetPropertyChanged;
        _notifier.Raised += OnNotice;
        SizeChanged += (_, _) => ApplyLayout();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        VersionText.Text = Loc.Format("VersionFormat", AppVersion());
        ThemeBox.ItemsSource = ThemeChoices.All.Select(c => Loc.Get(ThemeChoices.TextKey(c))).ToList();
        ThemeBox.SelectedIndex = (int)ThemeSettings.Load();
    }

    /// <summary>Wybór motywu — zapis w ustawieniach urządzenia; obowiązuje od następnego uruchomienia.</summary>
    void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeBox.SelectedIndex < 0 || (ThemeChoice)ThemeBox.SelectedIndex == ThemeSettings.Load()) return;
        ThemeSettings.Save((ThemeChoice)ThemeBox.SelectedIndex);
        _notifier.Info(Loc.Get("ThemeRestartNotice"));
    }

    async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_initialized)
        {
            _initialized = true;
            _vm.Initialize(portrait: ActualHeight > ActualWidth);
        }
        UpdateToolbar();
        ApplyLayout();
        UpdateLive();
        try
        {
            await _fleet.LoadAsync();
        }
        catch (SessionExpiredException)
        {
            // Obsługa w App.SessionExpired
        }
    }

    void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _vm.PropertyChanged -= OnShellPropertyChanged;
        _fleet.PropertyChanged -= OnFleetPropertyChanged;
        _notifier.Raised -= OnNotice;
    }

    void OnToggle(object sender, RoutedEventArgs e)
    {
        switch ((sender as FrameworkElement)?.Tag as string)
        {
            case "objects": _vm.ShowObjects = !_vm.ShowObjects; break;
            case "details": _vm.ShowDetails = !_vm.ShowDetails; break;
            case "map": _vm.ShowMap = !_vm.ShowMap; break;
            case "areas": _vm.ShowAreas = !_vm.ShowAreas; _fleet.ShowAreas = _vm.ShowAreas; break;
            case "drivers": _vm.ShowDrivers = !_vm.ShowDrivers; break;
            case "events": _vm.ShowEvents = !_vm.ShowEvents; break;
            case "messages": _vm.ShowMessages = !_vm.ShowMessages; break;
            case "route": _vm.ShowRouteReport = !_vm.ShowRouteReport; break;
            case "fleet": _vm.ShowFleetReport = !_vm.ShowFleetReport; break;
            case "eco": _vm.ShowEcoDriving = !_vm.ShowEcoDriving; break;
            case "logbooks": _vm.ShowLogbooks = !_vm.ShowLogbooks; break;
            case "equipment": _vm.ShowEquipment = !_vm.ShowEquipment; break;
        }
    }

    void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateToolbar();
        ApplyLayout();
    }

    void UpdateToolbar()
    {
        foreach (var b in Toolbar.Children.OfType<ToolButton>())
        {
            b.IsCompact = _vm.IsCompact;
            b.IsChecked = (b.Tag as string) switch
            {
                "objects" => _vm.ShowObjects,
                "details" => _vm.ShowDetails,
                "map" => _vm.ShowMap,
                "areas" => _vm.ShowAreas,
                "drivers" => _vm.ShowDrivers,
                "events" => _vm.ShowEvents,
                "messages" => _vm.ShowMessages,
                "route" => _vm.ShowRouteReport,
                "fleet" => _vm.ShowFleetReport,
                "eco" => _vm.ShowEcoDriving,
                "logbooks" => _vm.ShowLogbooks,
                "equipment" => _vm.ShowEquipment,
                _ => false,
            };
        }
    }

    /// <summary>Rozmieszczenie paneli zależnie od szerokości okna i włączonych paneli.</summary>
    void ApplyLayout()
    {
        if (ActualWidth <= 0) return;
        var compact = ActualWidth < CompactWidth || ActualHeight > ActualWidth;
        if (_vm.IsCompact != compact)
        {
            _vm.IsCompact = compact;
            return; // ponowne wywołanie przez PropertyChanged
        }

        ObjectsPanel.Visibility = Vis(_vm.ShowObjects);
        DetailsPanel.Visibility = Vis(_vm.ShowDetails);
        AreasPanel.Visibility = Vis(_vm.ShowAreas);
        DriversPanel.Visibility = Vis(_vm.ShowDrivers);
        MapPanel.Visibility = Vis(_vm.ShowMap);
        EventsPanel.Visibility = Vis(_vm.ShowEvents);
        MessagesPanel.Visibility = Vis(_vm.ShowMessages);
        RouteReportPanel.Visibility = Vis(_vm.ShowRouteReport);
        FleetReportPanel.Visibility = Vis(_vm.ShowFleetReport);
        EcoDrivingPanel.Visibility = Vis(_vm.ShowEcoDriving);
        LogbooksPanel.Visibility = Vis(_vm.ShowLogbooks);
        EquipmentPanel.Visibility = Vis(_vm.ShowEquipment);

        UIElement[] leftPanels = [ObjectsPanel, DetailsPanel, AreasPanel, DriversPanel];
        UIElement[] rightPanels = [EventsPanel, MessagesPanel, RouteReportPanel, FleetReportPanel, EcoDrivingPanel, LogbooksPanel, EquipmentPanel];

        Workspace.ColumnDefinitions.Clear();
        Workspace.RowDefinitions.Clear();

        if (compact)
        {
            // Panele w jednej kolumnie pod mapą
            MoveAll(leftPanels.Concat(rightPanels), CompactHost);
            ObjectsPanel.Height = 360;
            LeftHost.Visibility = Visibility.Collapsed;
            RightScroll.Visibility = Visibility.Collapsed;
            var anyPanel = _vm.AnyLeft || _vm.AnyRight;
            CompactScroll.Visibility = Vis(anyPanel);
            Workspace.RowDefinitions.Add(new RowDefinition { Height = _vm.ShowMap ? new GridLength(anyPanel ? 45 : 100, GridUnitType.Star) : new GridLength(0) });
            Workspace.RowDefinitions.Add(new RowDefinition { Height = anyPanel ? new GridLength(55, GridUnitType.Star) : new GridLength(0) });
            Grid.SetRow(MapPanel, 0);
            Grid.SetColumn(MapPanel, 0);
            Grid.SetRow(CompactScroll, 1);
            Grid.SetColumn(CompactScroll, 0);
        }
        else
        {
            MoveTo(ObjectsPanel, LeftHost, 0);
            MoveTo(DetailsPanel, LeftHost, 1);
            MoveTo(AreasPanel, LeftHost, 2);
            MoveTo(DriversPanel, LeftHost, 3);
            for (var i = 0; i < rightPanels.Length; i++) MoveTo(rightPanels[i], RightHost, i);
            ObjectsPanel.Height = double.NaN;
            LeftHost.RowDefinitions[0].Height = _vm.ShowObjects ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
            CompactScroll.Visibility = Visibility.Collapsed;

            // Siatka 12 kolumn: lewa 3, mapa 9 lub 4, prawa 5
            var left = _vm.AnyLeft ? 3 : 0;
            var right = _vm.AnyRight ? 5 : 0;
            var map = _vm.ShowMap ? (right > 0 ? 4 : 9) : 0;
            if (map == 0 && left > 0 && right == 0) left = 12;
            if (map == 0 && right > 0) right = 12 - left;
            Workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(left, GridUnitType.Star), MinWidth = left > 0 ? 300 : 0 });
            Workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(map, GridUnitType.Star) });
            Workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(right, GridUnitType.Star), MinWidth = right > 0 ? 420 : 0 });
            LeftHost.Visibility = Vis(left > 0);
            RightScroll.Visibility = Vis(right > 0);
            Grid.SetRow(MapPanel, 0);
            Grid.SetColumn(LeftHost, 0);
            Grid.SetColumn(MapPanel, 1);
            Grid.SetColumn(RightScroll, 2);
        }
    }

    static void MoveAll(IEnumerable<UIElement> elements, Panel target)
    {
        var i = 0;
        foreach (var e in elements) MoveTo(e, target, i++);
    }

    static void MoveTo(UIElement element, Panel target, int index)
    {
        if (VisualTreeHelper.GetParent(element) is Panel parent && parent != target) parent.Children.Remove(element);
        if (!target.Children.Contains(element))
        {
            if (target is Grid) target.Children.Add(element);
            else target.Children.Insert(Math.Min(index, target.Children.Count), element);
        }
        if (target is Grid) Grid.SetRow((FrameworkElement)element, index);
        else
        {
            var at = target.Children.IndexOf(element);
            var desired = Math.Min(index, target.Children.Count - 1);
            if (at != desired) target.Children.Move((uint)at, (uint)desired);
        }
    }

    void OnFleetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(FleetViewModel.LiveState):
            case nameof(FleetViewModel.IsStale):
                UpdateLive();
                break;
            case nameof(FleetViewModel.IsLoading):
                LoadingRing.IsActive = _fleet.IsLoading;
                break;
            case nameof(FleetViewModel.User):
                UserNameText.Text = _fleet.User?.DisplayName ?? string.Empty;
                UserCompanyText.Text = _fleet.User?.CompanyName ?? string.Empty;
                break;
        }
    }

    void UpdateLive()
    {
        var (brush, text) = _fleet.LiveState switch
        {
            LiveConnectionState.Connected => ("StatusMovingBrush", "LiveConnected"),
            LiveConnectionState.Connecting or LiveConnectionState.Reconnecting => ("StatusStoppedBrush", "LiveConnecting"),
            _ => ("StatusNoGpsBrush", "LiveDisconnected"),
        };
        if (_fleet.IsStale) text = "LiveStale";
        LiveDot.Fill = (Brush)Application.Current.Resources[brush];
        LiveText.Text = Loc.Get(text);
        LiveText.Visibility = Vis(!_vm.IsCompact);
        ToolTipService.SetToolTip(LiveDot, Loc.Get(text));
    }

    void OnNotice(object? sender, Notice n)
    {
        var bar = new InfoBar
        {
            Title = n.Title,
            Message = n.Message,
            Severity = n.Severity switch
            {
                NoticeSeverity.Error => InfoBarSeverity.Error,
                NoticeSeverity.Warning => InfoBarSeverity.Warning,
                NoticeSeverity.Success => InfoBarSeverity.Success,
                _ => InfoBarSeverity.Informational,
            },
            IsOpen = true,
            IsClosable = true,
        };
        bar.CloseButtonClick += (_, _) => Notices.Children.Remove(bar);
        Notices.Children.Add(bar);
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(n.Severity == NoticeSeverity.Error ? 12 : 6);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => Notices.Children.Remove(bar);
        timer.Start();
    }

    void OnMenuClick(object sender, RoutedEventArgs e) => Sidebar.IsPaneOpen = !Sidebar.IsPaneOpen;

    async void OnLogoutClick(object sender, RoutedEventArgs e)
    {
        Sidebar.IsPaneOpen = false;
        await App.Current.SignOutAsync();
    }

    static Visibility Vis(bool v) => v ? Visibility.Visible : Visibility.Collapsed;

    static string AppVersion()
    {
        try
        {
            var v = Windows.ApplicationModel.Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
        catch (InvalidOperationException)
        {
            return typeof(App).Assembly.GetName().Version?.ToString(3) ?? string.Empty;
        }
    }
}
