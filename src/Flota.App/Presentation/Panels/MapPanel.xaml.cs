using BruTile.Cache;
using BruTile.Predefined;
using BruTile.Web;
using Flota.App.Presentation.Fleet;
using Flota.App.Services;
using Flota.Core.Contracts;
using Flota.Core.Status;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using NetTopologySuite.Geometries;
using Windows.Storage;
using Brush = Mapsui.Styles.Brush;
using Color = Mapsui.Styles.Color;
using Pen = Mapsui.Styles.Pen;

namespace Flota.App.Presentation.Panels;

/// <summary>
/// Mapa floty (Mapsui): kafelki OSM z pamięcią podręczną, obszary, ślad (linia i strzałki kierunku), znaczniki obiektów,
/// okno informacji, menu kontekstowe widoku początkowego (odpowiednik mapy Leaflet w <c>Index-a.razor</c>).
/// </summary>
public sealed partial class MapPanel : UserControl
{
    const double TopResolution = 156543.03392804097;
    const int MaxTracePoints = 5000;
    /// <summary>Strzałki kierunku widoczne od tej rozdzielczości (ok. powiększenie 13) — czytelność i wydajność.</summary>
    const double ArrowsMaxResolution = 20;

    static readonly (double Lat, double Lon) DefaultCenter = (52.308479309, 20.162054062);

    readonly FleetViewModel _fleet;
    readonly MapImages _images;
    readonly Mapsui.Map _map = new();
    readonly MemoryLayer _areas = new("Obszary");
    readonly MemoryLayer _traceLine = new("Ślad");
    readonly MemoryLayer _arrows = new("Strzałki") { MaxVisible = ArrowsMaxResolution };
    readonly MemoryLayer _objects = new("Obiekty");
    readonly Dictionary<int, PointFeature> _objectFeatures = [];
    Coordinate[] _traceCoordinates = [];
    double _traceResolution;
    DispatcherQueueTimer? _simplifyTimer;
    bool _initialized;

    public MapPanel()
    {
        InitializeComponent();
        _fleet = App.Services.GetRequiredService<FleetViewModel>();
        _images = App.Services.GetRequiredService<MapImages>();
        Loaded += OnLoaded;
    }

    async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        await _images.LoadAsync();

        _map.BackColor = Col("SurfaceVariantColor");
        _map.Layers.Add(CreateTileLayer());
        _areas.Style = null;
        _traceLine.Style = null;
        _arrows.Style = null;
        _objects.Style = null;
        _map.Layers.Add(_areas);
        _map.Layers.Add(_traceLine);
        _map.Layers.Add(_arrows);
        _map.Layers.Add(_objects);
        _map.Tapped += OnMapTapped;
        _map.Navigator.ViewportChanged += (_, _) => OnViewportChanged();
        Map.Map = _map;

        var (x, y) = SphericalMercator.FromLonLat(DefaultCenter.Lon, DefaultCenter.Lat);
        _map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), Resolution(6));

        _fleet.ObjectsReset += (_, _) => RebuildObjects();
        _fleet.ObjectsUpdated += (_, items) => UpdateObjects(items);
        _fleet.TraceChanged += (_, _) => RebuildTrace();
        _fleet.AreasChanged += (_, _) => RebuildAreas();
        _fleet.CenterRequested += (_, c) => CenterOn(c.Lat, c.Lon, c.Zoom);
        _fleet.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FleetViewModel.SelectedObject)) UpdateObjects([]);
        };

        RebuildAreas();
        RebuildObjects();
        RebuildTrace();
        _fleet.GoToDefaultView();
    }

    TileLayer CreateTileLayer()
    {
        var config = App.Services.GetRequiredService<IOptions<AppConfig>>().Value;
        var cacheDir = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "tiles");
        Directory.CreateDirectory(cacheDir);
        var source = new HttpTileSource(
            new GlobalSphericalMercator(),
            config.TileUrl,
            name: "OpenStreetMap",
            persistentCache: new FileCache(cacheDir, "png", TimeSpan.FromDays(14)),
            attribution: new BruTile.Attribution("© OpenStreetMap contributors", "https://www.openstreetmap.org/copyright"),
            configureHttpRequestMessage: r => r.Headers.UserAgent.ParseAdd("Flota/1.0"));
        return new TileLayer(source) { Name = "OSM" };
    }

    // ---------------- Obiekty ----------------

    void RebuildObjects()
    {
        _objectFeatures.Clear();
        UpdateObjects(_fleet.AllObjects);
    }

    /// <summary>Aktualizacja znaczników; w trybie śladu widoczny tylko obiekt śledzony (jak w aplikacji źródłowej).</summary>
    void UpdateObjects(IReadOnlyList<ObjectItem> changed)
    {
        if (!_initialized) return;
        var traced = _fleet.TraceMode != TraceMode.None ? _fleet.SelectedObject : null;
        foreach (var item in changed.Count > 0 ? changed : _fleet.AllObjects)
        {
            var show = item.IsVisibleOnMap && item.Position is { HasLocation: true } && (traced is null || traced == item);
            if (!show)
            {
                _objectFeatures.Remove(item.Id);
                continue;
            }
            var position = traced == item && TracePosition() is { } tp ? tp : item.Position!;
            var (x, y) = SphericalMercator.FromLonLat(position.Longitude, position.Latitude);
            if (!_objectFeatures.TryGetValue(item.Id, out var f))
            {
                f = new PointFeature(x, y) { Data = item };
                _objectFeatures[item.Id] = f;
            }
            else
            {
                f.Point.X = x;
                f.Point.Y = y;
            }
            StyleObject(f, item, traced == item && _fleet.TraceMode == TraceMode.Report);
        }
        // Zaznaczenie mogło się zmienić — ponowne style wszystkich
        if (changed.Count > 0)
            foreach (var (id, f) in _objectFeatures)
                if (f.Data is ObjectItem it && !changed.Contains(it)) StyleObject(f, it, traced == it && _fleet.TraceMode == TraceMode.Report);

        _objects.Features = _objectFeatures.Values.OrderBy(f => ((ObjectItem)f.Data!).IsSelected).ToList();
        _objects.FeaturesWereModified();
        _map.RefreshGraphics();
    }

    void StyleObject(PointFeature f, ObjectItem item, bool reportMode)
    {
        var selected = item.IsSelected;
        var status = reportMode
            ? (TracePosition()?.IsEngineStarted == true ? TrackingObjectStatus.Move : TrackingObjectStatus.Stop)
            : item.Status;
        var unknown = status == TrackingObjectStatus.Unknown;
        var fill = Col(StatusColorKey(status));
        if (unknown) fill = new Color(fill.R, fill.G, fill.B, 128);
        var diameter = selected ? 38.0 : 36.0;

        f.Styles.Clear();
        f.Styles.Add(new SymbolStyle
        {
            SymbolType = SymbolType.Ellipse,
            SymbolScale = diameter / 32.0,
            Fill = new Brush(fill),
            Outline = selected
                ? new Pen(Col("FlotaSelectionOutlineColor"), 2)
                : unknown ? new Pen(Col("StatusNoGpsColor"), 1) : new Pen(new Color(0, 0, 0, 0), 0),
        });
        if (_images.ObjectIcon(item.Type) is { } icon)
        {
            f.Styles.Add(new ImageStyle
            {
                Image = new Mapsui.Styles.Image { Source = icon, BlendModeColor = unknown ? Col("StatusNoGpsColor") : null },
                SymbolScale = diameter / 64.0 * 0.9,
                Opacity = unknown ? 0.5f : 1f,
            });
        }
        f.Styles.Add(new LabelStyle
        {
            Text = item.Name,
            Font = new Font { Size = 12, Bold = selected },
            ForeColor = selected ? Col("FlotaTooltipSelectedColor") : Col("OnSurfaceColor"),
            BackColor = new Brush(WithAlpha(Col("SurfaceColor"), selected ? 255 : 204)),
            BorderColor = Col("OutlineVariantColor"),
            BorderThickness = 1,
            CornerRounding = 3,
            HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Left,
            Offset = new Offset(diameter / 2 + 4, 0),
            CollisionDetection = false,
        });
    }

    static string StatusColorKey(TrackingObjectStatus s) => s switch
    {
        TrackingObjectStatus.Move => "StatusMovingColor",
        TrackingObjectStatus.Stop => "StatusStoppedColor",
        TrackingObjectStatus.InvalidGps => "StatusNoGpsColor",
        TrackingObjectStatus.Outdated => "StatusOutdatedColor",
        _ => "StatusUnknownColor",
    };

    // ---------------- Ślad ----------------

    ObjectPosition? TracePosition()
    {
        var t = _fleet.Trace;
        if (t.Count == 0) return null;
        var i = _fleet.TraceCurrentIndex;
        return i >= 0 && i < t.Count ? t[i] : t[^1];
    }

    void RebuildTrace()
    {
        if (!_initialized) return;
        var points = _fleet.Trace;
        var arrows = new List<IFeature>();
        _traceCoordinates = [];
        if (points.Count > 1 && points.Count <= MaxTracePoints)
        {
            _traceCoordinates = points.Select(p =>
            {
                var (x, y) = SphericalMercator.FromLonLat(p.Longitude, p.Latitude);
                return new Coordinate(x, y);
            }).ToArray();

            for (var i = 0; i < points.Count - 1; i++)
            {
                var p = points[i];
                var (x, y) = SphericalMercator.FromLonLat(p.Longitude, p.Latitude);
                var f = new PointFeature(x, y) { Data = p };
                var dot = p.Speed is null or 0;
                var src = dot ? _images.Dot : _images.Arrow;
                if (src is not null)
                {
                    f.Styles.Add(new ImageStyle
                    {
                        Image = new Mapsui.Styles.Image { Source = src },
                        SymbolRotation = dot ? 0 : p.Heading ?? 0,
                        Opacity = 0.6f,
                    });
                }
                arrows.Add(f);
            }
        }
        _arrows.Features = arrows;
        _arrows.FeaturesWereModified();
        RebuildTraceLine();
        UpdateObjects([]);

        // Przesunięcie mapy do punktu bieżącego (raport) lub ostatniego (śledzenie)
        if (TracePosition() is { HasLocation: true } cur) CenterOn(cur.Latitude, cur.Longitude, null);
    }

    /// <summary>Linia śladu uproszczona do rozdzielczości ekranu (prototyp T0.4 — długie linie są głównym kosztem rysowania).</summary>
    void RebuildTraceLine()
    {
        var features = new List<IFeature>();
        if (_traceCoordinates.Length > 1)
        {
            _traceResolution = _map.Navigator.Viewport.Resolution;
            NetTopologySuite.Geometries.Geometry line = new LineString(_traceCoordinates);
            if (_traceCoordinates.Length > 500 && _traceResolution > 0)
                line = NetTopologySuite.Simplify.DouglasPeuckerSimplifier.Simplify(line, _traceResolution * 0.5);
            var color = Col("FlotaRouteLineColor");
            features.Add(new GeometryFeature(line)
            {
                Styles =
                {
                    new VectorStyle
                    {
                        Line = new Pen(new Color(color.R, color.G, color.B, 153), 10) { PenStrokeCap = PenStrokeCap.Butt, StrokeJoin = StrokeJoin.Round },
                    },
                },
            });
        }
        _traceLine.Features = features;
        _traceLine.FeaturesWereModified();
        _map.RefreshGraphics();
    }

    void OnViewportChanged()
    {
        if (_traceCoordinates.Length <= 500) return;
        var r = _map.Navigator.Viewport.Resolution;
        if (_traceResolution > 0 && Math.Abs(Math.Log(r / _traceResolution)) < Math.Log(1.5)) return;
        _simplifyTimer ??= CreateSimplifyTimer();
        _simplifyTimer.Stop();
        _simplifyTimer.Start();
    }

    DispatcherQueueTimer CreateSimplifyTimer()
    {
        var t = DispatcherQueue.CreateTimer();
        t.Interval = TimeSpan.FromMilliseconds(300);
        t.IsRepeating = false;
        t.Tick += (_, _) => RebuildTraceLine();
        return t;
    }

    // ---------------- Obszary ----------------

    void RebuildAreas()
    {
        if (!_initialized) return;
        var features = new List<IFeature>();
        if (_fleet.ShowAreas)
        {
            foreach (var a in _fleet.Areas.Where(a => a.IsActive && a.Outline.Count > 2))
            {
                var coords = a.Outline.Select(p =>
                {
                    var (x, y) = SphericalMercator.FromLonLat(p.Longitude, p.Latitude);
                    return new Coordinate(x, y);
                }).ToList();
                if (!coords[0].Equals2D(coords[^1])) coords.Add(coords[0]);
                var fill = Color.FromString(a.FillColor);
                var stroke = Color.FromString(a.OutlineColor);
                features.Add(new GeometryFeature(new Polygon(new LinearRing(coords.ToArray())))
                {
                    Data = a,
                    Styles =
                    {
                        // Wartości domyślne Leaflet: wypełnienie 20%, obrys 3 px
                        new VectorStyle { Fill = new Brush(new Color(fill.R, fill.G, fill.B, 51)), Outline = new Pen(stroke, 3) },
                        new LabelStyle { Text = a.Name, Font = new Font { Size = 11 }, ForeColor = Col("OnSurfaceColor"),
                                         BackColor = new Brush(WithAlpha(Col("SurfaceColor"), 204)), CollisionDetection = true },
                    },
                });
            }
        }
        _areas.Features = features;
        _areas.FeaturesWereModified();
        _map.RefreshGraphics();
    }

    // ---------------- Interakcja ----------------

    void OnMapTapped(object? sender, MapEventArgs e)
    {
        var info = e.GetMapInfo([_objects, _arrows]);
        switch (info?.Feature?.Data)
        {
            case ObjectItem item:
                if (_fleet.SelectedObject != item) _fleet.Select(item, center: false);
                ShowPopup(item.Name, item.RegistrationNumber, item.Position ?? TracePosition(), item.Source.DriverName, e.ScreenPosition);
                e.Handled = true;
                break;
            case ObjectPosition p when _fleet.SelectedObject is { } sel:
                ShowPopup(sel.Name, sel.RegistrationNumber, p, sel.Source.DriverName, e.ScreenPosition);
                e.Handled = true;
                break;
            default:
                Popup.Visibility = Visibility.Collapsed;
                break;
        }
    }

    /// <summary>Okno informacji (treść jak w oknie Leaflet aplikacji źródłowej).</summary>
    void ShowPopup(string name, string registration, ObjectPosition? p, string driverName, Mapsui.Manipulations.ScreenPosition at)
    {
        PopupContent.Children.Clear();
        var title = new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 13 };
        title.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = name, Foreground = Res<Microsoft.UI.Xaml.Media.Brush>("FlotaPopupNameBrush") });
        title.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = " " + registration, Foreground = Res<Microsoft.UI.Xaml.Media.Brush>("FlotaPopupRegistrationBrush") });
        PopupContent.Children.Add(title);
        Line($"{Loc.Get("PopupDriver")} {p?.DriverName ?? driverName}");
        Separator();
        if (p?.Speed is { } s) Line($"{Loc.Get("PopupSpeed")} {Loc.N0(s)} km/h");
        if (p?.SpeedMax is { } sm) Line($"{Loc.Get("PopupSpeedMax")} {Loc.N0(sm)} km/h");
        if (p?.FuelLevel is { } fl) Line($"{Loc.Get("PopupFuel")} {Loc.N0(fl)} l");
        Line($"{Loc.Get("PopupIgnition")} {(p?.IsEngineStarted == true ? Loc.Get("Yes") : Loc.Get("No"))}");
        Separator();
        if (p?.Address is { IsEmpty: false } a)
        {
            Line($"{Loc.Get("PopupAddress")} {a.FullAddress}");
            Separator();
        }
        if (p is not null) Line(Loc.DateTimeShort(p.Time));

        Popup.Visibility = Visibility.Visible;
        Popup.Measure(new Windows.Foundation.Size(300, double.PositiveInfinity));
        var w = Popup.DesiredSize.Width;
        var h = Popup.DesiredSize.Height;
        var left = Math.Clamp(at.X - w / 2, 4, Math.Max(4, MapHost.ActualWidth - w - 4));
        var top = at.Y - h - 26;
        if (top < 4) top = at.Y + 26;
        Canvas.SetLeft(Popup, left);
        Canvas.SetTop(Popup, Math.Clamp(top, 4, Math.Max(4, MapHost.ActualHeight - h - 4)));

        void Line(string text) => PopupContent.Children.Add(new TextBlock { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        void Separator() => PopupContent.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 4, 0, 4), Background = Res<Microsoft.UI.Xaml.Media.Brush>("OutlineVariantBrush") });
    }

    void OnPopupClose(object sender, RoutedEventArgs e) => Popup.Visibility = Visibility.Collapsed;

    void CenterOn(double lat, double lon, double? zoom)
    {
        if (!_initialized) return;
        var (x, y) = SphericalMercator.FromLonLat(lon, lat);
        if (zoom is { } z) _map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), Resolution(z), 300);
        else _map.Navigator.CenterOn(new MPoint(x, y), 300);
    }

    void OnZoomIn(object sender, RoutedEventArgs e) => _map.Navigator.ZoomIn(250);

    void OnZoomOut(object sender, RoutedEventArgs e) => _map.Navigator.ZoomOut(250);

    async void OnSaveDefaultView(object sender, RoutedEventArgs e)
    {
        var v = _map.Navigator.Viewport;
        var (lon, lat) = SphericalMercator.ToLonLat(v.CenterX, v.CenterY);
        await _fleet.SaveDefaultViewAsync(lat, lon, Math.Round(Zoom(v.Resolution), 2));
    }

    void OnGoDefaultView(object sender, RoutedEventArgs e) => _fleet.GoToDefaultView();

    async void OnDeleteDefaultView(object sender, RoutedEventArgs e) => await _fleet.DeleteDefaultViewAsync();

    static double Resolution(double zoom) => TopResolution / Math.Pow(2, zoom);

    static double Zoom(double resolution) => Math.Log2(TopResolution / resolution);

    /// <summary>Kolor z zasobów palety (Styles/ColorPaletteOverride.xaml).</summary>
    static Color Col(string key)
    {
        var c = Application.Current.Resources.TryGetValue(key, out var v) && v is Windows.UI.Color wc ? wc : Microsoft.UI.Colors.Magenta;
        return new Color(c.R, c.G, c.B, c.A);
    }

    static Color WithAlpha(Color c, int a) => new(c.R, c.G, c.B, a);

    static T Res<T>(string key) => (T)Application.Current.Resources[key];
}
