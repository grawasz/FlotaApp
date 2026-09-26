using System.Collections;
using System.Globalization;
using System.Text;
using Flota.App.Services;
using Flota.Core.Contracts;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;

namespace Flota.App.Controls;

/// <summary>Kolumna tabeli.</summary>
public sealed class TableColumn
{
    public required string Header { get; init; }
    /// <summary>Tekst komórki (sformatowany, pl-PL).</summary>
    public required Func<object, string?> Text { get; init; }
    /// <summary>Klucz sortowania; domyślnie tekst komórki.</summary>
    public Func<object, IComparable?>? SortKey { get; init; }
    /// <summary>Wartość do eksportu (kultura niezmienna); domyślnie tekst komórki.</summary>
    public Func<object, string?>? ExportValue { get; init; }
    public ExportColumnKind ExportKind { get; init; } = ExportColumnKind.Text;
    public double Width { get; init; } = 120;
    public TextAlignment Alignment { get; init; } = TextAlignment.Left;
    public bool Wrap { get; init; }
}

/// <summary>Wiersz tabeli (komórki jako właściwości — wiązanie bez indeksatorów, zgodne ze wszystkimi platformami).</summary>
[Bindable]
public sealed partial class TableRow(object item, string?[] cells)
{
    public object Item { get; } = item;
    internal string?[] Cells { get; } = cells;

    string? C(int i) => i < Cells.Length ? Cells[i] : null;
#pragma warning disable CA1707
    public string? C0 => C(0); public string? C1 => C(1); public string? C2 => C(2); public string? C3 => C(3);
    public string? C4 => C(4); public string? C5 => C(5); public string? C6 => C(6); public string? C7 => C(7);
    public string? C8 => C(8); public string? C9 => C(9); public string? C10 => C(10); public string? C11 => C(11);
    public string? C12 => C(12); public string? C13 => C(13); public string? C14 => C(14); public string? C15 => C(15);
    public string? C16 => C(16); public string? C17 => C(17); public string? C18 => C(18); public string? C19 => C(19);
    public string? C20 => C(20); public string? C21 => C(21); public string? C22 => C(22); public string? C23 => C(23);
    public string? C24 => C(24); public string? C25 => C(25); public string? C26 => C(26); public string? C27 => C(27);
    public string? C28 => C(28); public string? C29 => C(29); public string? C30 => C(30); public string? C31 => C(31);
    public string? C32 => C(32); public string? C33 => C(33); public string? C34 => C(34); public string? C35 => C(35);
    public string? C36 => C(36); public string? C37 => C(37); public string? C38 => C(38); public string? C39 => C(39);
#pragma warning restore CA1707
}

/// <summary>
/// Tabela danych: nagłówki z sortowaniem, wirtualizowana lista wierszy, szybki filtr, stopka, eksport.
/// Zastępuje <c>RadzenDataGrid</c> na wszystkich platformach (plan D9; wynik prototypu T0.5).
/// </summary>
public sealed partial class FlotaTable : UserControl
{
    public const int MaxColumns = 40;

    readonly Grid _root = new();
    readonly Grid _header = new();
    readonly ListView _list = new();
    readonly TextBox _filter = new();
    readonly TextBlock _empty = new();
    readonly TextBlock _footer = new();
    readonly ScrollViewer _hscroll = new();

    List<TableRow> _allRows = [];
    List<TableRow> _visibleRows = [];
    int _sortColumn = -1;
    bool _sortDescending;
    bool _suppressSelection;

    public FlotaTable()
    {
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _filter.PlaceholderText = Loc.Get("TableFilterPlaceholder");
        _filter.Margin = new Thickness(0, 0, 0, 4);
        _filter.Visibility = Visibility.Collapsed;
        _filter.TextChanged += (_, _) => ApplyView(keepSelection: true);
        AutomationProperties.SetName(_filter, Loc.Get("TableFilterPlaceholder"));
        Grid.SetRow(_filter, 0);
        _root.Children.Add(_filter);

        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _header.Background = Brush("FlotaToolbarBrush");
        body.Children.Add(_header);
        Grid.SetRow(_list, 1);
        _list.SelectionMode = ListViewSelectionMode.Single;
        _list.IsItemClickEnabled = false;
        _list.HorizontalAlignment = HorizontalAlignment.Left;
        _list.SelectionChanged += OnListSelectionChanged;
        _list.ItemContainerStyle = RowContainerStyle();
        body.Children.Add(_list);
        _empty.Text = Loc.Get("NoData");
        _empty.Margin = new Thickness(8, 12, 8, 12);
        _empty.Visibility = Visibility.Collapsed;
        _empty.Style = Style("FlotaCaptionTextStyle");
        Grid.SetRow(_empty, 1);
        body.Children.Add(_empty);

        _hscroll.Content = body;
        _hscroll.HorizontalScrollMode = ScrollMode.Auto;
        _hscroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        _hscroll.VerticalScrollMode = ScrollMode.Disabled;
        _hscroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Grid.SetRow(_hscroll, 1);
        _root.Children.Add(_hscroll);

        _footer.Margin = new Thickness(6, 4, 6, 2);
        _footer.Style = Style("FlotaCaptionTextStyle");
        _footer.Visibility = Visibility.Collapsed;
        Grid.SetRow(_footer, 2);
        _root.Children.Add(_footer);

        Content = _root;
    }

    /// <summary>Zmiana zaznaczenia (pojedynczego lub wielokrotnego).</summary>
    public event EventHandler<IReadOnlyList<object>>? SelectionChanged;

    public IReadOnlyList<TableColumn> Columns { get; private set; } = [];

    public static readonly DependencyProperty ShowFilterProperty = DependencyProperty.Register(
        nameof(ShowFilter), typeof(bool), typeof(FlotaTable), new PropertyMetadata(false, (d, e) =>
            ((FlotaTable)d)._filter.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed));

    public bool ShowFilter
    {
        get => (bool)GetValue(ShowFilterProperty);
        set => SetValue(ShowFilterProperty, value);
    }

    public static readonly DependencyProperty MultipleSelectionProperty = DependencyProperty.Register(
        nameof(MultipleSelection), typeof(bool), typeof(FlotaTable), new PropertyMetadata(false, (d, e) =>
            ((FlotaTable)d)._list.SelectionMode = (bool)e.NewValue ? ListViewSelectionMode.Multiple : ListViewSelectionMode.Single));

    public bool MultipleSelection
    {
        get => (bool)GetValue(MultipleSelectionProperty);
        set => SetValue(MultipleSelectionProperty, value);
    }

    public static readonly DependencyProperty FooterTextProperty = DependencyProperty.Register(
        nameof(FooterText), typeof(string), typeof(FlotaTable), new PropertyMetadata(null, (d, e) => ((FlotaTable)d).UpdateFooter()));

    /// <summary>Szablon stopki z <c>{0}</c> — liczba widocznych wierszy (np. „Łącznie: {0}”).</summary>
    public string? FooterText
    {
        get => (string?)GetValue(FooterTextProperty);
        set => SetValue(FooterTextProperty, value);
    }

    public IReadOnlyList<object> Items => _allRows.Select(r => r.Item).ToList();

    public IReadOnlyList<object> SelectedItems => _list.SelectedItems.OfType<TableRow>().Select(r => r.Item).ToList();

    public object? SelectedItem => (_list.SelectedItem as TableRow)?.Item;

    /// <summary>Kolumny i dane tabeli.</summary>
    public void SetData(IReadOnlyList<TableColumn> columns, IEnumerable items)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(items);
        if (!ReferenceEquals(columns, Columns))
        {
            Columns = columns.Take(MaxColumns).ToList();
            _sortColumn = -1;
            BuildHeader();
            _list.ItemTemplate = BuildTemplate(Columns);
        }
        _allRows = items.Cast<object>().Select(i => new TableRow(i, Columns.Select(c => Safe(c.Text, i)).ToArray())).ToList();
        ApplyView(keepSelection: false);
    }

    public void Clear() => SetData(Columns, Array.Empty<object>());

    /// <summary>Zaznaczenie elementów (bez zgłaszania zdarzenia).</summary>
    public void Select(IEnumerable<object> items)
    {
        var set = items.ToHashSet();
        _suppressSelection = true;
        try
        {
            if (_list.SelectionMode == ListViewSelectionMode.Multiple)
            {
                _list.SelectedItems.Clear();
                foreach (var r in _visibleRows.Where(r => set.Contains(r.Item))) _list.SelectedItems.Add(r);
            }
            else
            {
                var row = _visibleRows.FirstOrDefault(r => set.Contains(r.Item));
                _list.SelectedItem = row;
                if (row is not null) _list.ScrollIntoView(row);
            }
        }
        finally
        {
            _suppressSelection = false;
        }
    }

    /// <summary>Tabela do eksportu: widoczne kolumny i wiersze w bieżącej kolejności.</summary>
    public ExportTable ToExport(string sheetName) => new(
        sheetName,
        Columns.Select(c => new ExportColumn(c.Header, c.ExportKind)).ToList(),
        _visibleRows.Select(r => (IReadOnlyList<string?>)Columns.Select((c, i) => c.ExportValue is null ? r.Cells[i] : Safe(c.ExportValue, r.Item)).ToList()).ToList());

    void ApplyView(bool keepSelection)
    {
        var selected = keepSelection ? SelectedItems.ToHashSet() : [];
        IEnumerable<TableRow> rows = _allRows;
        var filter = _filter.Text?.Trim();
        if (!string.IsNullOrEmpty(filter))
            rows = rows.Where(r => r.Cells.Any(c => c is not null && c.Contains(filter, StringComparison.CurrentCultureIgnoreCase)));
        if (_sortColumn >= 0 && _sortColumn < Columns.Count)
        {
            var col = Columns[_sortColumn];
            var comparer = Comparer<IComparable?>.Create(CompareKeys);
            rows = _sortDescending
                ? rows.OrderByDescending(r => Key(col, r, _sortColumn), comparer)
                : rows.OrderBy(r => Key(col, r, _sortColumn), comparer);
        }
        _visibleRows = rows.ToList();
        _suppressSelection = true;
        _list.ItemsSource = _visibleRows;
        _suppressSelection = false;
        _empty.Visibility = _visibleRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (selected.Count > 0) Select(selected);
        UpdateFooter();
    }

    static IComparable? Key(TableColumn col, TableRow r, int i) => col.SortKey is null ? r.Cells[i] : col.SortKey(r.Item);

    static int CompareKeys(IComparable? a, IComparable? b)
    {
        if (a is null) return b is null ? 0 : -1;
        if (b is null) return 1;
        if (a is string sa && b is string sb) return string.Compare(sa, sb, Loc.Pl, CompareOptions.IgnoreCase);
        try { return a.CompareTo(b); }
        catch (ArgumentException) { return string.Compare(a.ToString(), b.ToString(), StringComparison.CurrentCulture); }
    }

    void UpdateFooter()
    {
        var f = FooterText;
        _footer.Visibility = string.IsNullOrEmpty(f) ? Visibility.Collapsed : Visibility.Visible;
        if (!string.IsNullOrEmpty(f)) _footer.Text = string.Format(Loc.Pl, f, _visibleRows.Count);
    }

    void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection) return;
        SelectionChanged?.Invoke(this, SelectedItems);
    }

    void BuildHeader()
    {
        _header.Children.Clear();
        _header.ColumnDefinitions.Clear();
        var total = 0.0;
        for (var i = 0; i < Columns.Count; i++)
        {
            var col = Columns[i];
            total += col.Width;
            _header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(col.Width) });
            var text = new TextBlock
            {
                Text = col.Header,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = col.Alignment,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Brush("OnSurfaceBrush"),
                MaxLines = 2,
            };
            var arrow = new FontIcon { FontSize = 9, Glyph = string.Empty, Margin = new Thickness(4, 0, 0, 0), FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"] };
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(text);
            panel.Children.Add(arrow);
            var button = new Button
            {
                Content = panel,
                Tag = i,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = col.Alignment == TextAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                Padding = new Thickness(6, 4, 6, 4),
                MinHeight = 32,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = Brush("OutlineVariantBrush"),
                CornerRadius = new CornerRadius(0),
            };
            if (Application.Current.Resources.TryGetValue("TextButtonStyle", out var s) && s is Style st) button.Style = st;
            button.Click += OnHeaderClick;
            AutomationProperties.SetName(button, col.Header);
            Grid.SetColumn(button, i);
            _header.Children.Add(button);
        }
        _header.Width = total;
        _list.Width = total + 4;
    }

    void OnHeaderClick(object sender, RoutedEventArgs e)
    {
        var i = (int)((Button)sender).Tag;
        if (_sortColumn == i) _sortDescending = !_sortDescending;
        else
        {
            _sortColumn = i;
            _sortDescending = false;
        }
        foreach (var b in _header.Children.OfType<Button>())
            if (b.Content is StackPanel p && p.Children[1] is FontIcon icon)
                icon.Glyph = (int)b.Tag == _sortColumn ? (_sortDescending ? "" : "") : string.Empty;
        ApplyView(keepSelection: true);
    }

    static DataTemplate BuildTemplate(IReadOnlyList<TableColumn> columns)
    {
        var sb = new StringBuilder();
        sb.Append("<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
        sb.Append("<Grid MinHeight=\"28\">");
        sb.Append("<Grid.ColumnDefinitions>");
        foreach (var c in columns)
            sb.Append(CultureInfo.InvariantCulture, $"<ColumnDefinition Width=\"{c.Width}\"/>");
        sb.Append("</Grid.ColumnDefinitions>");
        for (var i = 0; i < columns.Count; i++)
        {
            var c = columns[i];
            var align = c.Alignment switch { TextAlignment.Right => "Right", TextAlignment.Center => "Center", _ => "Left" };
            var wrap = c.Wrap ? "Wrap" : "NoWrap";
            sb.Append(CultureInfo.InvariantCulture,
                $"<TextBlock Grid.Column=\"{i}\" Text=\"{{Binding C{i}}}\" FontSize=\"12\" Margin=\"6,3\" VerticalAlignment=\"Center\" TextAlignment=\"{align}\" TextWrapping=\"{wrap}\" TextTrimming=\"CharacterEllipsis\"/>");
        }
        sb.Append("</Grid></DataTemplate>");
        return (DataTemplate)XamlReader.Load(sb.ToString());
    }

    static Style RowContainerStyle()
    {
        var style = new Style(typeof(ListViewItem));
        style.Setters.Add(new Setter(MinHeightProperty, 28.0));
        style.Setters.Add(new Setter(PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("OutlineVariantBrush")));
        return style;
    }

    static string? Safe(Func<object, string?> f, object item)
    {
        try
        {
            return f(item);
        }
        catch (Exception ex) when (ex is NullReferenceException or InvalidCastException or FormatException or ArgumentException)
        {
            return null;
        }
    }

    static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    static Style? Style(string key) => Application.Current.Resources.TryGetValue(key, out var s) ? s as Style : null;
}
