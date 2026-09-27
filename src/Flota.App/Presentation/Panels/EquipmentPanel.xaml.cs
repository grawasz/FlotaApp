using Flota.App.Controls;
using Flota.App.Services;
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

    readonly EquipmentViewModel _vm = App.Services.GetRequiredService<EquipmentViewModel>();

    public EquipmentPanel()
    {
        InitializeComponent();
        Table.SetData(Columns, Array.Empty<object>());
        ExportButton.IsEnabled = false;
        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(EquipmentViewModel.Items):
                    Table.SetData(Columns, _vm.Items);
                    ExportButton.IsEnabled = _vm.Items.Count > 0;
                    break;
                case nameof(EquipmentViewModel.IsBusy):
                    Busy.IsActive = _vm.IsBusy;
                    RefreshButton.IsEnabled = !_vm.IsBusy;
                    break;
            }
        };
        RegisterPropertyChangedCallback(VisibilityProperty, async (_, _) =>
        {
            if (Visibility == Visibility.Visible) await _vm.EnsureLoadedAsync();
        });
    }

    async void OnRefreshClick(object sender, RoutedEventArgs e) => await _vm.RefreshAsync();

    async void OnExportClick(object sender, RoutedEventArgs e) =>
        await App.Services.GetRequiredService<ExportService>().ExportAsync(Table.ToExport(Loc.Get("SheetEquipment")), _vm.FileName);
}
