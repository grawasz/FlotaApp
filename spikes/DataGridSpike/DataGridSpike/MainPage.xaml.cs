using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DataGridSpike;

// Prototyp T0.5: DataGrid z 10 000 wierszy punktów trasy i dynamicznymi kolumnami czujników
// (odpowiednik Reports.razor — GetWaypoints). Pomiar automatyczny, wynik zapisywany do pliku.
public sealed partial class MainPage : Page
{
    const int RowCount = 10_000;
    const int ScrollSeconds = 10;

#if WINDOWS
    const string Variant = "winappsdk";
#else
    const string Variant = "skia";
#endif

    static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");

    // Kolumny stałe jak w Reports.razor (waypointCols)
    static readonly string[] FixedColumns =
        ["Data", "Prędkość [km/h]", "Obroty", "Stan licznika [km]", "Miasto", "Ulica", "Stacyjka"];

    static readonly string[] SensorSetA =
        ["Paliwo [l]", "Temp. silnika [C]", "Temp. zew. [C]", "Temp. wew. [C]", "Drzwi", "Hamulec",
         "Bieg neutralny", "Napięcie [V]", "Obciążenie [%]", "Pedał gazu [%]", "Tachograf", "Alkotest"];

    static readonly string[] SensorSetB =
        ["Paliwo [l]", "Napięcie [V]", "Drzwi", "Chłodnia [C]", "Wywrot", "PTO", "Pług", "Posypywarka"];

    static readonly string[] Cities = ["Warszawa", "Piaseczno", "Grójec", "Otwock", "Pruszków", "Legionowo", "Radom"];
    static readonly string[] Streets = ["Puławska", "Marszałkowska", "Al. Jerozolimskie", "Grójecka", "Modlińska", "Wał Miedzeszyński"];

    readonly StringBuilder _log = new();
    readonly Random _rnd = new(7);
    DataGrid _grid = null!;
    List<Row> _rows = [];
    int _loadedRows;

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
        Report($"Platforma: {Platform()}");
        var mem0 = WorkingSetMb();

        var sw = Stopwatch.StartNew();
        _rows = GenerateRows(SensorSetA.Length);
        Report($"Generowanie {RowCount} wierszy: {sw.ElapsedMilliseconds} ms");

        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserSortColumns = true,
            CanUserReorderColumns = true,
            CanUserResizeColumns = true,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            SelectionMode = DataGridSelectionMode.Single,
        };
        _grid.Sorting += OnSorting;
        _grid.LoadingRow += (_, _) => _loadedRows++;
        Host.Child = _grid;
        await NextFrames(2);

        sw.Restart();
        BuildColumns(SensorSetA);
        var tCols = sw.ElapsedMilliseconds;
        sw.Restart();
        _loadedRows = 0;
        _grid.ItemsSource = _rows;
        await RowsVisible();
        Report($"Kolumny ({FixedColumns.Length} stałych + {SensorSetA.Length} czujników): {tCols} ms; pierwsze wyświetlenie {RowCount} wierszy: {sw.ElapsedMilliseconds} ms ({_loadedRows} wierszy wczytanych)");
        Report($"Pamięć robocza: przed {mem0:F0} MB, po wyświetleniu {WorkingSetMb():F0} MB");

        await Task.Delay(1000);
        await SnapshotAsync("1-wyswietlenie");
        await MeasureScroll("Przewijanie wierszami (SmallIncrement na klatkę)", ScrollAmount.SmallIncrement);
        await MeasureScroll("Przewijanie stronami (LargeIncrement na klatkę)", ScrollAmount.LargeIncrement);
        await SnapshotAsync("2-po-przewijaniu");

        // Sortowanie jak po kliknięciu nagłówka (malejąco wg prędkości)
        sw.Restart();
        var sorted = _rows.OrderByDescending(r => r.Speed).ToList();
        var tSort = sw.ElapsedMilliseconds;
        sw.Restart();
        _loadedRows = 0;
        _grid.ItemsSource = sorted;
        await RowsVisible();
        Report($"Sortowanie 10 000 wierszy: {tSort} ms; ponowne wyświetlenie: {sw.ElapsedMilliseconds} ms ({_loadedRows} wierszy wczytanych)");

        // Zmiana zestawu czujników (inny obiekt w raporcie)
        sw.Restart();
        var rowsB = GenerateRows(SensorSetB.Length);
        var tGen = sw.ElapsedMilliseconds;
        sw.Restart();
        _loadedRows = 0;
        _grid.ItemsSource = null;
        BuildColumns(SensorSetB);
        _grid.ItemsSource = rowsB;
        await RowsVisible();
        Report($"Zmiana zestawu kolumn ({SensorSetA.Length} → {SensorSetB.Length} czujników): generowanie {tGen} ms, przebudowa i wyświetlenie {sw.ElapsedMilliseconds} ms ({_loadedRows} wierszy wczytanych)");
        _rows = rowsB;
        await Task.Delay(500);
        await SnapshotAsync("3-po-zmianie-kolumn");

        await MeasureScroll("Przewijanie wierszami po zmianie kolumn", ScrollAmount.SmallIncrement);
        Report($"Pamięć robocza na końcu: {WorkingSetMb():F0} MB");
        Report("KONIEC POMIARU");
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(), "DataGridSpike-wynik.txt"), _log.ToString());
    }

    // Przewijanie przez wzorzec automatyzacji Scroll — ta sama ścieżka co pasek przewijania
    async Task MeasureScroll(string name, ScrollAmount amount)
    {
        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(_grid);
        if (peer?.GetPattern(PatternInterface.Scroll) is not IScrollProvider sp)
        {
            Report($"{name}: wzorzec Scroll niedostępny");
            return;
        }
        sp.SetScrollPercent(ScrollPatternIdentifiers.NoScroll, 0);
        await NextFrames(2);
        var frames = 0;
        var maxHandler = 0.0;
        _loadedRows = 0;
        var h = Stopwatch.StartNew();
        var p0 = sp.VerticalScrollPercent;
        EventHandler<object> handler = (_, _) =>
        {
            h.Restart();
            if (sp.VerticalScrollPercent < 99.9) sp.Scroll(ScrollAmount.NoAmount, amount);
            maxHandler = Math.Max(maxHandler, h.Elapsed.TotalMilliseconds);
            frames++;
        };
        var sw = Stopwatch.StartNew();
        CompositionTarget.Rendering += handler;
        await Task.Delay(TimeSpan.FromSeconds(ScrollSeconds));
        CompositionTarget.Rendering -= handler;
        var s = sw.Elapsed.TotalSeconds;
        Report($"{name}: {frames / s:F1} kl./s, położenie {p0:F1}% → {sp.VerticalScrollPercent:F1}%, wczytane wiersze {_loadedRows} ({_loadedRows / s:F0}/s), maks. czas obsługi klatki {maxHandler:F1} ms");
    }

    async Task RowsVisible()
    {
        var sw = Stopwatch.StartNew();
        while (_loadedRows == 0 && sw.ElapsedMilliseconds < 5000) await NextFrames(1);
        await NextFrames(1);
    }

    // Zrzut obszaru tabeli do pliku BMP (weryfikacja wizualna)
    async Task SnapshotAsync(string name)
    {
        try
        {
            var rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(Host);
            var px = (await rtb.GetPixelsAsync()).ToArray();
            int w = rtb.PixelWidth, h = rtb.PixelHeight;
            var path = Path.Combine(Path.GetTempPath(), $"DataGridSpike-{Variant}-{name}.bmp");
            using var fs = File.Create(path);
            using var bw = new BinaryWriter(fs);
            bw.Write((ushort)0x4D42); bw.Write(54 + px.Length); bw.Write(0); bw.Write(54);
            bw.Write(40); bw.Write(w); bw.Write(-h); bw.Write((ushort)1); bw.Write((ushort)32);
            bw.Write(0); bw.Write(px.Length); bw.Write(2835); bw.Write(2835); bw.Write(0); bw.Write(0);
            bw.Write(px);
        }
        catch (Exception ex)
        {
            Report($"Zrzut {name}: {ex.GetType().Name} {ex.Message}");
        }
    }

    void BuildColumns(string[] sensors)
    {
        _grid.Columns.Clear();
        var names = FixedColumns.Concat(sensors).ToArray();
        for (int i = 0; i < names.Length; i++)
        {
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = names[i],
                Binding = new Binding { Path = new PropertyPath($"Cells[{i}]") },
                Tag = i,
            });
        }
    }

    void OnSorting(object? sender, DataGridColumnEventArgs e)
    {
        var i = (int)e.Column.Tag;
        var desc = e.Column.SortDirection == DataGridSortDirection.Ascending;
        var src = (IList<Row>)_grid.ItemsSource;
        _grid.ItemsSource = desc
            ? src.OrderByDescending(r => r.SortKey(i)).ToList()
            : src.OrderBy(r => r.SortKey(i)).ToList();
        foreach (var c in _grid.Columns) c.SortDirection = null;
        e.Column.SortDirection = desc ? DataGridSortDirection.Descending : DataGridSortDirection.Ascending;
    }

    List<Row> GenerateRows(int sensorCount)
    {
        var list = new List<Row>(RowCount);
        var t = new DateTime(2026, 9, 25, 6, 0, 0);
        double odo = 184_320;
        for (int i = 0; i < RowCount; i++)
        {
            t = t.AddSeconds(5 + _rnd.Next(0, 10));
            var speed = _rnd.Next(0, 91);
            odo += speed * 10 / 3600.0;
            var cells = new string[FixedColumns.Length + sensorCount];
            cells[0] = t.ToString("yyyy-MM-dd HH:mm:ss", Pl);
            cells[1] = speed.ToString(Pl);
            cells[2] = (speed == 0 ? 800 : 1200 + _rnd.Next(0, 1400)).ToString(Pl);
            cells[3] = odo.ToString("N1", Pl);
            cells[4] = Cities[_rnd.Next(Cities.Length)];
            cells[5] = Streets[_rnd.Next(Streets.Length)] + " " + _rnd.Next(1, 200);
            cells[6] = speed > 0 || _rnd.NextDouble() < 0.5 ? "tak" : "nie";
            for (int s = 0; s < sensorCount; s++)
                cells[FixedColumns.Length + s] = (_rnd.NextDouble() * 100).ToString("N1", Pl);
            list.Add(new Row(cells, speed));
        }
        return list;
    }

    static Task NextFrames(int n)
    {
        var tcs = new TaskCompletionSource();
        var count = 0;
        EventHandler<object>? h = null;
        h = (_, _) => { if (++count >= n) { CompositionTarget.Rendering -= h; tcs.TrySetResult(); } };
        CompositionTarget.Rendering += h;
        return tcs.Task;
    }

    static double WorkingSetMb()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        return Process.GetCurrentProcess().WorkingSet64 / 1024.0 / 1024.0;
    }

    void Report(string line)
    {
        _log.AppendLine(line);
        Status.Text = _log.ToString();
        Console.WriteLine($"DATAGRIDSPIKE {line}");
    }

    static string Platform() =>
#if WINDOWS
        $"WinAppSDK, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}, {Environment.ProcessorCount} rdzeni";
#else
        $"Uno Skia, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}, {Environment.ProcessorCount} rdzeni";
#endif

    [Bindable]
    public sealed class Row(string[] cells, int speed)
    {
        public string[] Cells { get; } = cells;
        public int Speed { get; } = speed;

        public IComparable SortKey(int i) =>
            i == 1 ? Speed :
            double.TryParse(Cells[i], NumberStyles.Number, Pl, out var d) ? d : Cells[i];
    }
}
