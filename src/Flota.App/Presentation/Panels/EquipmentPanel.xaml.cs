using Flota.App.Controls;
using Flota.App.Services;
using Flota.Client;
using Flota.Core.Contracts;

namespace Flota.App.Presentation.Panels;

/// <summary>Panel „Wyposażenie” z eksportem XLSX.</summary>
public sealed partial class EquipmentPanel : UserControl
{
    static TableColumn Count(string header, Func<EquipmentInfo, int> v) => new()
    {
        Header = Loc.Get(header),
        Text = o => v((EquipmentInfo)o).ToString(Loc.Pl),
        SortKey = o => v((EquipmentInfo)o),
        ExportValue = o => Loc.Raw(v((EquipmentInfo)o)),
        ExportKind = ExportColumnKind.WholeNumber,
        Width = 100,
        Alignment = TextAlignment.Right,
    };

    static readonly IReadOnlyList<TableColumn> Columns =
    [
        new() { Header = Loc.Get("ColRegistration"), Text = o => ((EquipmentInfo)o).RegistrationNumber, Width = 130 },
        new() { Header = Loc.Get("ColName"), Text = o => ((EquipmentInfo)o).ObjectName, Width = 160 },
        new() { Header = Loc.Get("ColDevice"), Text = o => ((EquipmentInfo)o).DeviceName, Width = 110 },
        Count("ColFuelProbe", e => e.FuelProbes),
        Count("ColAlcoDetector", e => e.AlcoDetectors),
        Count("ColVideoRecorder", e => e.VideoRecorders),
        Count("ColCamera", e => e.Cameras),
        Count("ColMonitor", e => e.Monitors),
    ];

    bool _loaded;

    public EquipmentPanel()
    {
        InitializeComponent();
        Table.SetData(Columns, Array.Empty<object>());
        ExportButton.IsEnabled = false;
        RegisterPropertyChangedCallback(VisibilityProperty, async (_, _) =>
        {
            if (Visibility == Visibility.Visible && !_loaded) await LoadAsync(refresh: false);
        });
    }

    async void OnRefreshClick(object sender, RoutedEventArgs e) => await LoadAsync(refresh: true);

    async Task LoadAsync(bool refresh)
    {
        _loaded = true;
        Busy.IsActive = true;
        RefreshButton.IsEnabled = false;
        try
        {
            var items = await App.Services.GetRequiredService<FlotaApiClient>().GetEquipmentAsync(refresh, CancellationToken.None);
            Table.SetData(Columns, items);
            ExportButton.IsEnabled = items.Count > 0;
        }
        catch (Exception ex) when (ex is not SessionExpiredException)
        {
            _loaded = false;
            App.Services.GetRequiredService<Notifier>().Error(ex);
        }
        finally
        {
            Busy.IsActive = false;
            RefreshButton.IsEnabled = true;
        }
    }

    async void OnExportClick(object sender, RoutedEventArgs e) =>
        await App.Services.GetRequiredService<ExportService>().ExportAsync(
            Table.ToExport(Loc.Get("SheetEquipment")),
            $"{Loc.Get("FileEquipment")} {DateTime.Now:yyyyMMdd_HHmm}.xlsx");
}
