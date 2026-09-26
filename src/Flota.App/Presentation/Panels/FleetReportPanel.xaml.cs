using Flota.App.Controls;
using Flota.App.Presentation.Fleet;
using Flota.App.Services;
using Flota.Client;
using Flota.Core.Contracts;

namespace Flota.App.Presentation.Panels;

/// <summary>
/// Panel „Raport floty”: podsumowanie na obiekt i szczegóły dzienne; kolumny widoczne tylko wtedy,
/// gdy przynajmniej jeden wiersz ma wartość (jak w aplikacji źródłowej).
/// </summary>
public sealed partial class FleetReportPanel : UserControl
{
    readonly FleetViewModel _fleet;
    FleetReportResponse? _report;

    public FleetReportPanel()
    {
        InitializeComponent();
        _fleet = App.Services.GetRequiredService<FleetViewModel>();
        FromPicker.Date = DateTimeOffset.Now.Date.AddDays(-1);
        ToPicker.Date = DateTimeOffset.Now.Date;
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
            if (rows.Count > 0 && rows.All(r => v(r) is null)) return;
            list.Add(new() { Header = Loc.Get(h), Text = o => fmt(v((FleetReportRow)o)), SortKey = o => v((FleetReportRow)o),
                             ExportValue = o => Loc.Raw(v((FleetReportRow)o)), ExportKind = ExportColumnKind.Number, Width = 120, Alignment = TextAlignment.Right });
        }
        void Txt(string h, Func<FleetReportRow, string?> v, double width = 110)
        {
            if (rows.Count > 0 && rows.All(r => string.IsNullOrEmpty(v(r)))) return;
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
        var ids = Picker.SelectedIds;
        if (ids.Count == 0)
        {
            App.Services.GetRequiredService<Notifier>().Info(Loc.Get("PickObjectsFirst"));
            return;
        }
        if (FromPicker.Date is not { } from || ToPicker.Date is not { } to) return;
        Busy.IsActive = true;
        GenerateButton.IsEnabled = false;
        try
        {
            _report = await App.Services.GetRequiredService<FlotaApiClient>().GetFleetReportAsync(new FleetReportRequest(ids, from.Date, to.Date), CancellationToken.None);
            SummaryTable.SetData(Columns(_report.Summary, details: false), _report.Summary);
            DetailsTable.SetData(Columns(_report.Details, details: true), _report.Details);
            ExportSummaryButton.IsEnabled = _report.Summary.Count > 0;
            ExportDetailsButton.IsEnabled = _report.Details.Count > 0;
        }
        catch (Exception ex) when (ex is not SessionExpiredException)
        {
            App.Services.GetRequiredService<Notifier>().Error(ex);
        }
        finally
        {
            Busy.IsActive = false;
            GenerateButton.IsEnabled = true;
        }
    }

    async void OnExportSummaryClick(object sender, RoutedEventArgs e) =>
        await App.Services.GetRequiredService<ExportService>().ExportAsync(SummaryTable.ToExport(Loc.Get("SheetFleetSummary")), $"{FileBase()}.xlsx");

    async void OnExportDetailsClick(object sender, RoutedEventArgs e) =>
        await App.Services.GetRequiredService<ExportService>().ExportAsync(DetailsTable.ToExport(Loc.Get("SheetFleetDetails")), $"{FileBase()} {Loc.Get("FileDetailsSuffix")}.xlsx");

    string FileBase() => $"{Loc.Get("FileFleetReport")} {FromPicker.Date:yyyyMMdd} {ToPicker.Date:yyyyMMdd}";
}
