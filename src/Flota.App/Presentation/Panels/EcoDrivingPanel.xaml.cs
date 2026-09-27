using Flota.App.Controls;
using Flota.App.Services;
using Flota.Core.Contracts;
using Flota.Core.Reports;

namespace Flota.App.Presentation.Panels;

/// <summary>Panel „Ranking Eco-Driving”: ocena stylu jazdy obiektów w okresie.</summary>
public sealed partial class EcoDrivingPanel : UserControl
{
    static TableColumn Score(string header, Func<EcoDrivingRow, double?> v) => new()
    {
        Header = Loc.Get(header), Text = o => Loc.N0(v((EcoDrivingRow)o)), SortKey = o => v((EcoDrivingRow)o),
        ExportValue = o => Loc.Raw(v((EcoDrivingRow)o)), ExportKind = ExportColumnKind.Number, Width = 110, Alignment = TextAlignment.Right,
    };

    static readonly IReadOnlyList<TableColumn> Columns =
    [
        new() { Header = Loc.Get("ColPosition"), Text = o => ((EcoDrivingRow)o).Position.ToString(Loc.Pl), SortKey = o => ((EcoDrivingRow)o).Position, Width = 70,
                Alignment = TextAlignment.Right, ExportValue = o => Loc.Raw(((EcoDrivingRow)o).Position), ExportKind = ExportColumnKind.WholeNumber },
        new() { Header = Loc.Get("ColObject"), Text = o => ((EcoDrivingRow)o).ObjectName, Width = 150 },
        new() { Header = Loc.Get("ColRegistration"), Text = o => ((EcoDrivingRow)o).RegistrationNumber, Width = 120 },
        Score("ColEcoScore", r => r.Score),
        Score("ColEcoSpeeding", r => r.Speeding),
        Score("ColEcoRpm", r => r.ExceedRpm),
        Score("ColEcoBraking", r => r.SuddenBraking),
        Score("ColEcoAcceleration", r => r.QuickAcceleration),
        new() { Header = Loc.Get("ColSpeedingTime"), Text = o => ReportRules.FormatDuration(((EcoDrivingRow)o).SpeedingTime), SortKey = o => ((EcoDrivingRow)o).SpeedingTime, Width = 120, Alignment = TextAlignment.Right },
        new() { Header = Loc.Get("ColExceedRpmTime"), Text = o => ReportRules.FormatDuration(((EcoDrivingRow)o).ExceedRpmTime), SortKey = o => ((EcoDrivingRow)o).ExceedRpmTime, Width = 120, Alignment = TextAlignment.Right },
        new() { Header = Loc.Get("ColSuddenBrakings"), Text = o => ((EcoDrivingRow)o).SuddenBrakings?.ToString(Loc.Pl), SortKey = o => ((EcoDrivingRow)o).SuddenBrakings, Width = 110,
                Alignment = TextAlignment.Right, ExportValue = o => Loc.Raw(((EcoDrivingRow)o).SuddenBrakings), ExportKind = ExportColumnKind.WholeNumber },
        new() { Header = Loc.Get("ColQuickAccelerations"), Text = o => ((EcoDrivingRow)o).QuickAccelerations?.ToString(Loc.Pl), SortKey = o => ((EcoDrivingRow)o).QuickAccelerations, Width = 110,
                Alignment = TextAlignment.Right, ExportValue = o => Loc.Raw(((EcoDrivingRow)o).QuickAccelerations), ExportKind = ExportColumnKind.WholeNumber },
    ];

    readonly FleetViewModel _fleet;
    readonly EcoDrivingViewModel _vm = App.Services.GetRequiredService<EcoDrivingViewModel>();

    public EcoDrivingPanel()
    {
        InitializeComponent();
        _fleet = App.Services.GetRequiredService<FleetViewModel>();
        FromPicker.Date = _vm.From;
        ToPicker.Date = _vm.To;
        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(EcoDrivingViewModel.Rows):
                    Table.SetData(Columns, _vm.Rows);
                    ExportButton.IsEnabled = _vm.Rows.Count > 0;
                    break;
                case nameof(EcoDrivingViewModel.IsBusy):
                    Busy.IsActive = _vm.IsBusy;
                    GenerateButton.IsEnabled = !_vm.IsBusy;
                    break;
            }
        };
        _fleet.ObjectsReset += (_, _) => Picker.SetObjects(_fleet.FleetObjects);
        Picker.SetObjects(_fleet.FleetObjects);
        Table.SetData(Columns, Array.Empty<object>());
    }

    async void OnGenerateClick(object sender, RoutedEventArgs e)
    {
        if (FromPicker.Date is not { } from || ToPicker.Date is not { } to) return;
        _vm.From = from.Date;
        _vm.To = to.Date;
        await _vm.GenerateAsync(Picker.SelectedIds);
    }

    async void OnExportClick(object sender, RoutedEventArgs e) =>
        await App.Services.GetRequiredService<ExportService>().ExportAsync(Table.ToExport(Loc.Get("SheetEcoDriving")), _vm.FileName);
}
