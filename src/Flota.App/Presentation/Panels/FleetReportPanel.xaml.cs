using Flota.App.Controls;
using Flota.App.Services;
using Flota.Core.Contracts;

namespace Flota.App.Presentation.Panels;

/// <summary>
/// Panel „Raport floty”: podsumowanie na obiekt i szczegóły dzienne; kolumny widoczne tylko wtedy,
/// gdy przynajmniej jeden wiersz ma wartość (jak w aplikacji źródłowej).
/// </summary>
public sealed partial class FleetReportPanel : UserControl
{
    readonly FleetViewModel _fleet;
    readonly FleetReportViewModel _vm = App.Services.GetRequiredService<FleetReportViewModel>();

    public FleetReportPanel()
    {
        InitializeComponent();
        _fleet = App.Services.GetRequiredService<FleetViewModel>();
        FromPicker.Date = _vm.From;
        ToPicker.Date = _vm.To;
        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(FleetReportViewModel.Summary):
                    SummaryTable.SetData(Columns(_vm.Summary, details: false), _vm.Summary);
                    ExportSummaryButton.IsEnabled = _vm.Summary.Count > 0;
                    break;
                case nameof(FleetReportViewModel.Details):
                    DetailsTable.SetData(Columns(_vm.Details, details: true), _vm.Details);
                    ExportDetailsButton.IsEnabled = _vm.Details.Count > 0;
                    break;
                case nameof(FleetReportViewModel.IsBusy):
                    Busy.IsActive = _vm.IsBusy;
                    GenerateButton.IsEnabled = !_vm.IsBusy;
                    break;
            }
        };
        _fleet.ObjectsReset += (_, _) => Picker.SetObjects(_fleet.FleetObjects);
        Picker.SetObjects(_fleet.FleetObjects);
        SummaryTable.SetData(Columns([], details: false), Array.Empty<object>());
        DetailsTable.SetData(Columns([], details: true), Array.Empty<object>());
    }

    /// <summary>Kolumny raportu; kolumny wartości tylko przy danych.</summary>
    static IReadOnlyList<TableColumn> Columns(IReadOnlyList<FleetReportRow> rows, bool details)
    {
        var list = new List<TableColumn>();
        if (details)
            list.Add(new() { Header = Loc.Get("ColDate"), Text = o => Loc.Date(((FleetReportRow)o).Date), SortKey = o => ((FleetReportRow)o).Date, Width = 100,
                             ExportValue = o => Loc.Raw(((FleetReportRow)o).Date), ExportKind = ExportColumnKind.Date });
        list.Add(new() { Header = Loc.Get("ColObject"), Text = o => ((FleetReportRow)o).ObjectName, Width = 150 });
        if (!details) list.Add(new() { Header = Loc.Get("ColMake"), Text = o => ((FleetReportRow)o).MakeModel, Width = 130 });

        void Num(string h, Func<FleetReportRow, double?> v, Func<double?, string> fmt)
        {
            if (!FleetReportViewModel.IsColumnVisible(rows, v)) return;
            list.Add(new() { Header = Loc.Get(h), Text = o => fmt(v((FleetReportRow)o)), SortKey = o => v((FleetReportRow)o),
                             ExportValue = o => Loc.Raw(v((FleetReportRow)o)), ExportKind = ExportColumnKind.Number, Width = 120, Alignment = TextAlignment.Right });
        }
        void Txt(string h, Func<FleetReportRow, string?> v, double width = 110)
        {
            if (!FleetReportViewModel.IsColumnVisible(rows, v)) return;
            list.Add(new() { Header = Loc.Get(h), Text = o => v((FleetReportRow)o), Width = width, Alignment = TextAlignment.Right });
        }

        Num("ColOdometer", r => r.Odometer, Loc.N0);
        Txt("ColRideTime", r => r.RideTime);
        Txt("ColStopTime", r => r.StopTime);
        Num("ColDistanceKm", r => r.Distance, Loc.N2);
        Num("ColAverageSpeedFleet", r => r.AverageSpeed, Loc.N0);
        Num("ColMaxSpeedFleet", r => r.MaxSpeed, Loc.N0);
        Num("ColMaxRpmFleet", r => r.MaxRpm, Loc.N0);
        Num("ColFuelUsed", r => r.FuelUsed, Loc.N2);
        Num("ColConsumption100", r => r.FuelConsumption, Loc.N2);
        Num("ColConsumptionHour", r => r.FuelConsumptionInTime, Loc.N2);
        Txt("ColActivityNeutral", r => r.NeutralGear, 130);
        Txt("ColActivityNeutralMoving", r => r.NeutralGearMoving, 130);
        Txt("ColActivityDoors", r => r.Doors, 130);
        Txt("ColActivityBrake", r => r.Brake, 130);
        return list;
    }

    void OnTabClick(object sender, RoutedEventArgs e)
    {
        var details = (string)((FrameworkElement)sender).Tag == "1";
        TabSummary.IsChecked = !details;
        TabDetails.IsChecked = details;
        SummaryTable.Visibility = details ? Visibility.Collapsed : Visibility.Visible;
        DetailsTable.Visibility = details ? Visibility.Visible : Visibility.Collapsed;
    }

    async void OnGenerateClick(object sender, RoutedEventArgs e)
    {
        if (FromPicker.Date is not { } from || ToPicker.Date is not { } to) return;
        _vm.From = from.Date;
        _vm.To = to.Date;
        await _vm.GenerateAsync(Picker.SelectedIds);
    }

    async void OnExportSummaryClick(object sender, RoutedEventArgs e) =>
        await App.Services.GetRequiredService<ExportService>().ExportAsync(SummaryTable.ToExport(Loc.Get("SheetFleetSummary")), $"{_vm.FileBase}.xlsx");

    async void OnExportDetailsClick(object sender, RoutedEventArgs e) =>
        await App.Services.GetRequiredService<ExportService>().ExportAsync(DetailsTable.ToExport(Loc.Get("SheetFleetDetails")), $"{_vm.FileBase} {Loc.Get("FileDetailsSuffix")}.xlsx");
}
