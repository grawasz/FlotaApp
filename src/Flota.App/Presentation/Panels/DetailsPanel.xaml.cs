using System.ComponentModel;
using Flota.App.Presentation.Fleet;
using Flota.App.Services;
using Flota.Core.Objects;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Flota.App.Presentation.Panels;

/// <summary>Panel „Szczegóły obiektu” (odpowiednik sekcji szczegółów <c>Index-a.razor</c>).</summary>
public sealed partial class DetailsPanel : UserControl
{
    readonly FleetViewModel _fleet;
    ObjectItem? _item;

    public DetailsPanel()
    {
        InitializeComponent();
        _fleet = App.Services.GetRequiredService<FleetViewModel>();
        _fleet.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FleetViewModel.SelectedObject)) Attach(_fleet.SelectedObject);
        };
        Attach(_fleet.SelectedObject);
    }

    void Attach(ObjectItem? item)
    {
        if (_item is not null) _item.PropertyChanged -= OnItemChanged;
        _item = item;
        if (_item is not null) _item.PropertyChanged += OnItemChanged;
        Build();
    }

    void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or "" or nameof(ObjectItem.Status)) Build();
    }

    void Build()
    {
        Rows.Children.Clear();
        EmptyText.Visibility = _item is null ? Visibility.Visible : Visibility.Collapsed;
        Scroll.Visibility = _item is null ? Visibility.Collapsed : Visibility.Visible;
        if (_item is null) return;

        var o = _item.Source;
        var m = o.Position;
        Text("DetailName", o.Name);
        Text("DetailRegistration", o.RegistrationNumber);
        if (!string.IsNullOrEmpty(o.Manufacturer)) Text("DetailManufacturer", o.Manufacturer);
        if (!string.IsNullOrEmpty(o.Model)) Text("DetailModel", o.Model);
        if (m?.Speed is { } speed) Text("DetailSpeed", $"{Loc.N0(speed)} km/h");
        if (m?.SpeedFromCan is { } can) Text("DetailSpeedCan", $"{Loc.N0(can)} km/h");
        if (m?.Rpm is { } rpm) Text("DetailRpm", $"{Loc.N0(rpm)} {Loc.Get("UnitRpm")}");
        if (m?.TotalDistanceCan is { } odo) Text("DetailOdometer", $"{Loc.N0(odo)} km");
        if (m?.Fuel is { } fuel) Text("DetailFuel", m.FuelLevel is { } l ? $"{Loc.N0(l)} l ({fuel.ToString("P0", Loc.Pl)})" : fuel.ToString("P0", Loc.Pl));
        if (_item.Enabled && m?.Address is { IsEmpty: false } a) Text("DetailAddress", a.FullAddress);
        Text("DetailArea", string.Empty);
        Icon("tracking_mode_public_32x32.png", Loc.Get("DetailPublicMode"));
        if (m?.Satellites is > 0) Icon("satelites_32x32.png", m.Satellites.Value.ToString(Loc.Pl));
        if (o.Type != TrackingObjectIcons.PortableRadioType)
            Icon(m?.IsEngineStarted == true ? "ignition_enabled_32x32.png" : "ignition_disabled_32x32.png", Loc.Get("DetailIgnition"));
        Text("DetailStatus", _item.StatusName);
        if (!string.IsNullOrEmpty(m?.DriverName ?? o.DriverName)) Text("DetailDriver", m?.DriverName ?? o.DriverName);
        Text("DetailDevice", o.DeviceName);

        foreach (var s in (m?.Sensors ?? []).Where(s => s.Visible).OrderBy(s => s.Order))
        {
            if (s.BoolValue is { } b) Sensor(b, s.Name);
            else Pair(s.Name, s.Value ?? string.Empty, bold: false);
        }

        if (m is not null) DateRow(m.Time);
    }

    void Text(string labelKey, string value) => Pair(Loc.Get(labelKey), value, bold: true);

    void Pair(string label, string value, bool bold)
    {
        var g = Row();
        g.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 13,
            FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            Foreground = bold ? Res<Brush>("FlotaAccentBrush") : Res<Brush>("OnSurfaceBrush"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top,
        });
        var v = new TextBlock { Text = value, FontSize = 13, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        Grid.SetColumn(v, 1);
        g.Children.Add(v);
        Rows.Children.Add(g);
    }

    void Icon(string image, string text)
    {
        var g = Row();
        g.Children.Add(new Image { Source = new BitmapImage(new Uri($"ms-appx:///Assets/Images/{image}")), Width = 24, Height = 24, HorizontalAlignment = HorizontalAlignment.Left });
        var t = new TextBlock { Text = text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(t, 1);
        g.Children.Add(t);
        Rows.Children.Add(g);
    }

    void Sensor(bool on, string name)
    {
        var g = Row();
        g.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = Res<Brush>(on ? "FlotaSensorOnBrush" : "FlotaSensorOffBrush"),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var t = new TextBlock { Text = name, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(t, 1);
        g.Children.Add(t);
        AutomationProperties.SetName(g, $"{name}: {(on ? Loc.Get("Yes") : Loc.Get("No"))}");
        Rows.Children.Add(g);
    }

    void DateRow(DateTime time)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
        p.Children.Add(new Image { Source = new BitmapImage(new Uri("ms-appx:///Assets/Images/calendars_16x16.png")), Width = 16, Height = 16 });
        p.Children.Add(new TextBlock { Text = Loc.Date(time), FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        p.Children.Add(new Image { Source = new BitmapImage(new Uri("ms-appx:///Assets/Images/clock_16x16.png")), Width = 16, Height = 16, Margin = new Thickness(12, 0, 0, 0) });
        p.Children.Add(new TextBlock { Text = Loc.Time(time), FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        Rows.Children.Add(p);
    }

    static Grid Row()
    {
        var g = new Grid { MinHeight = 26, ColumnSpacing = 8 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return g;
    }

    static T Res<T>(string key) => (T)Application.Current.Resources[key];
}
