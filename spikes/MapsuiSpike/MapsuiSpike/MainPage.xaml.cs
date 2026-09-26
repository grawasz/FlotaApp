using System.Diagnostics;
using System.Text;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Utilities;
using NetTopologySuite.Geometries;
using Brush = Mapsui.Styles.Brush;
using Color = Mapsui.Styles.Color;

namespace MapsuiSpike;

// Prototyp T0.4: 500 markerów z obrotem, linia 10 000 punktów, aktualizacja pozycji co 1 s.
// Po uruchomieniu wykonuje automatyczny pomiar w trzech fazach i zapisuje wynik.
public sealed partial class MainPage : Page
{
    const int MarkerCount = 500;
    const int LinePoints = 10_000;
    const int PhaseSeconds = 15;

    // Obszar testowy: okolice Warszawy (ok. 60 × 40 km)
    const double MinLon = 20.60, MaxLon = 21.40, MinLat = 52.05, MaxLat = 52.40;

    static readonly Color[] StatusColors =
    [
        Color.FromString("#008000"), Color.FromString("#FFA500"),
        Color.FromString("#808080"), Color.FromString("#FF0000"),
    ];

    readonly Random _rnd = new(42);
    readonly List<Marker> _markers = [];
    readonly MemoryLayer _markerLayer = new("Obiekty");
    Performance _perf = null!;
    readonly StringBuilder _log = new();
    DispatcherTimer? _updateTimer;
    int _updates;
    long _updateTicks;

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await RunAsync();
        }
        catch (Exception ex)
        {
            Report($"BŁĄD: {ex}");
        }
    }

    async Task RunAsync()
    {
        var sw = Stopwatch.StartNew();
        var map = new Map();
        _perf = map.Performance ?? throw new InvalidOperationException("Map.Performance niedostępne");
        _perf.IsActive = Mapsui.Widgets.ActiveMode.Yes;
        map.Layers.Add(OpenStreetMap.CreateTileLayer("FlotaUno-MapsuiSpike"));
        map.Layers.Add(CreateLineLayer());
        map.Layers.Add(CreateMarkerLayer());
        MapControl.Map = map;

        var (x1, y1) = SphericalMercator.FromLonLat(MinLon, MinLat);
        var (x2, y2) = SphericalMercator.FromLonLat(MaxLon, MaxLat);
        map.Navigator.ZoomToBox(new MRect(x1, y1, x2, y2));
        Report($"Platforma: {Platform()}");
        Report($"Budowa mapy i warstw: {sw.ElapsedMilliseconds} ms");

        // Rozgrzanie: pobranie kafelków
        await Task.Delay(TimeSpan.FromSeconds(5));
        _perf.Clear();

        // Faza 1: aktualizacja pozycji co 1 s, mapa nieruchoma
        StartUpdates();
        await Measure("F1 aktualizacja 1 s, mapa nieruchoma", TimeSpan.FromSeconds(PhaseSeconds), null);

        // Faza 2: ciągłe przesuwanie mapy + aktualizacja co 1 s (odpowiednik gestu przesuwania)
        var center = map.Navigator.Viewport.CenterX;
        var cy = map.Navigator.Viewport.CenterY;
        double t = 0;
        await Measure("F2 przesuwanie ciągłe + aktualizacja 1 s", TimeSpan.FromSeconds(PhaseSeconds), () =>
        {
            t += 0.02;
            var r = map.Navigator.Viewport.Resolution * 40;
            map.Navigator.CenterOn(center + Math.Cos(t) * r, cy + Math.Sin(t) * r);
        });

        // Faza 3: ciągłe powiększanie/pomniejszanie + aktualizacja co 1 s
        var res0 = map.Navigator.Viewport.Resolution;
        t = 0;
        await Measure("F3 zmiana powiększenia + aktualizacja 1 s", TimeSpan.FromSeconds(PhaseSeconds), () =>
        {
            t += 0.03;
            map.Navigator.ZoomTo(res0 * (1.0 + 0.6 * Math.Sin(t)));
        });

        // Fazy 4–6: rozkład kosztu rysowania na warstwy (przesuwanie ciągłe)
        map.Navigator.ZoomTo(res0);
        var line = map.Layers.First(l => l.Name == "Trasa");
        Action pan = () =>
        {
            t += 0.02;
            var r = map.Navigator.Viewport.Resolution * 40;
            map.Navigator.CenterOn(center + Math.Cos(t) * r, cy + Math.Sin(t) * r);
        };
        _markerLayer.Enabled = false;
        await Measure("F4 przesuwanie: kafelki + linia (bez markerów)", TimeSpan.FromSeconds(PhaseSeconds), pan);
        _markerLayer.Enabled = true; line.Enabled = false;
        await Measure("F5 przesuwanie: kafelki + markery (bez linii)", TimeSpan.FromSeconds(PhaseSeconds), pan);
        _markerLayer.Enabled = false;
        await Measure("F6 przesuwanie: same kafelki", TimeSpan.FromSeconds(PhaseSeconds), pan);
        _markerLayer.Enabled = true; line.Enabled = true;

        // Faza 7: linia uproszczona (Douglas-Peucker, tolerancja 0,5 px przy bieżącej rozdzielczości)
        var lineLayer = (MemoryLayer)line;
        var original = (GeometryFeature)lineLayer.Features.First();
        var sws = Stopwatch.StartNew();
        var simplified = NetTopologySuite.Simplify.DouglasPeuckerSimplifier.Simplify(original.Geometry, map.Navigator.Viewport.Resolution * 0.5);
        var simplifyMs = sws.Elapsed.TotalMilliseconds;
        lineLayer.Features = [new GeometryFeature(simplified)];
        lineLayer.FeaturesWereModified();
        Report($"Uproszczenie linii: {original.Geometry!.NumPoints} → {simplified.NumPoints} punktów w {simplifyMs:F1} ms");
        await Measure("F7 przesuwanie: wszystkie warstwy, linia uproszczona", TimeSpan.FromSeconds(PhaseSeconds), pan);

        Report($"Aktualizacje pozycji: {_updates}, średni czas aktualizacji danych: {(_updates == 0 ? 0 : TimeSpan.FromTicks(_updateTicks / _updates).TotalMilliseconds):F2} ms");
        Report("KONIEC POMIARU");
        await SaveAsync();
    }

    async Task Measure(string name, TimeSpan duration, Action? onFrame)
    {
        _perf.Clear();
        var sw = Stopwatch.StartNew();
        var frames = 0;
        EventHandler<object> handler = (_, _) => { frames++; onFrame?.Invoke(); };
        CompositionTarget.Rendering += handler;
        await Task.Delay(duration);
        CompositionTarget.Rendering -= handler;
        var s = sw.Elapsed.TotalSeconds;

        var drawn = _perf.Count;
        Report($"{name}: renderowania mapy {drawn} ({drawn / s:F1}/s), " +
               $"czas rysowania śr. {_perf.Mean:F1} ms, min {_perf.Min:F1}, maks. {_perf.Max:F1} ms " +
               $"→ maks. osiągalne {(_perf.Mean > 0 ? 1000 / _perf.Mean : 0):F0} kl./s; klatki interfejsu {frames / s:F1}/s");
    }

    void StartUpdates()
    {
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _updateTimer.Tick += (_, _) => UpdatePositions();
        _updateTimer.Start();
    }

    // Symulacja strumienia pozycji: każdy obiekt przesuwa się zgodnie z kursem, kurs losowo się zmienia
    void UpdatePositions()
    {
        var sw = Stopwatch.StartNew();
        foreach (var m in _markers)
        {
            m.Course = (m.Course + _rnd.Next(-20, 21) + 360) % 360;
            var rad = m.Course * Math.PI / 180;
            m.Feature.Point.X += Math.Sin(rad) * 15;  // ok. 15 m/s ≈ 54 km/h
            m.Feature.Point.Y += Math.Cos(rad) * 15;
            m.Style.SymbolRotation = m.Course;
            if (_rnd.NextDouble() < 0.02) m.Style.Fill = new Brush(StatusColors[_rnd.Next(StatusColors.Length)]);
        }
        _markerLayer.FeaturesWereModified();
        _updateTicks += sw.Elapsed.Ticks;
        _updates++;
        MapControl.Map.RefreshGraphics();
    }

    MemoryLayer CreateMarkerLayer()
    {
        for (int i = 0; i < MarkerCount; i++)
        {
            var (x, y) = SphericalMercator.FromLonLat(
                MinLon + _rnd.NextDouble() * (MaxLon - MinLon),
                MinLat + _rnd.NextDouble() * (MaxLat - MinLat));
            var course = _rnd.Next(0, 360);
            var style = new SymbolStyle
            {
                SymbolType = SymbolType.Triangle,
                SymbolScale = 0.5,
                SymbolRotation = course,
                Fill = new Brush(StatusColors[i % StatusColors.Length]),
                Outline = new Pen(Color.FromString("#27285C"), 1.5),
            };
            var f = new PointFeature(x, y);
            f.Styles.Add(style);
            _markers.Add(new Marker(f, style, course));
        }
        _markerLayer.Features = _markers.Select(m => (IFeature)m.Feature).ToList();
        _markerLayer.Style = null;
        return _markerLayer;
    }

    MemoryLayer CreateLineLayer()
    {
        // Ślad trasy: błądzenie losowe, 10 000 punktów
        var coords = new Coordinate[LinePoints];
        double lon = (MinLon + MaxLon) / 2, lat = (MinLat + MaxLat) / 2, dir = 0;
        for (int i = 0; i < LinePoints; i++)
        {
            dir += (_rnd.NextDouble() - 0.5) * 0.6;
            lon = Math.Clamp(lon + Math.Sin(dir) * 0.0004, MinLon, MaxLon);
            lat = Math.Clamp(lat + Math.Cos(dir) * 0.00025, MinLat, MaxLat);
            var (x, y) = SphericalMercator.FromLonLat(lon, lat);
            coords[i] = new Coordinate(x, y);
        }
        var feature = new GeometryFeature(new LineString(coords));
        return new MemoryLayer("Trasa")
        {
            Features = [feature],
            Style = new VectorStyle { Line = new Pen(Color.FromString("#3481E5"), 3) },
        };
    }

    void Report(string line)
    {
        _log.AppendLine(line);
        Status.Text = _log.ToString();
        Console.WriteLine($"MAPSUISPIKE {line}");
    }

    async Task SaveAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), "MapsuiSpike-wynik.txt");
        await File.WriteAllTextAsync(path, _log.ToString());
        Report($"Zapisano: {path}");
    }

    static string Platform()
    {
#if __ANDROID__
        return $"Android {Android.OS.Build.VERSION.Release} (API {(int)Android.OS.Build.VERSION.SdkInt}), {Android.OS.Build.Manufacturer} {Android.OS.Build.Model}, {Environment.ProcessorCount} rdzeni";
#else
        return $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription}, {Environment.ProcessorCount} rdzeni";
#endif
    }

    sealed class Marker(PointFeature feature, SymbolStyle style, double course)
    {
        public PointFeature Feature { get; } = feature;
        public SymbolStyle Style { get; } = style;
        public double Course { get; set; } = course;
    }
}
