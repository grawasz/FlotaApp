using Flota.App.Controls;
using Flota.App.Services;
using Flota.Core.Contracts;

namespace Flota.App.Presentation.Panels;

/// <summary>Panel „Karty drogowe”: lista kart w okresie i pozycje wybranej karty.</summary>
public sealed partial class LogbooksPanel : UserControl
{
    static TableColumn Int(string header, Func<LogbookInfo, int?> v) => new()
    {
        Header = Loc.Get(header), Text = o => Loc.N0(v((LogbookInfo)o)), SortKey = o => v((LogbookInfo)o),
        ExportValue = o => Loc.Raw(v((LogbookInfo)o)), ExportKind = ExportColumnKind.WholeNumber, Width = 110, Alignment = TextAlignment.Right,
    };

    static readonly IReadOnlyList<TableColumn> LogbookColumns =
    [
        new() { Header = Loc.Get("ColNumber"), Text = o => ((LogbookInfo)o).Id.ToString(Loc.Pl), SortKey = o => ((LogbookInfo)o).Id, Width = 80,
                ExportValue = o => Loc.Raw(((LogbookInfo)o).Id), ExportKind = ExportColumnKind.WholeNumber },
        new() { Header = Loc.Get("ColDate"), Text = o => Loc.DateTimeShort(((LogbookInfo)o).Date), SortKey = o => ((LogbookInfo)o).Date, Width = 130,
                ExportValue = o => Loc.Raw(((LogbookInfo)o).Date), ExportKind = ExportColumnKind.DateTime },
        new() { Header = Loc.Get("ColRegistration"), Text = o => ((LogbookInfo)o).RegistrationNumber, Width = 130 },
        Int("ColBeginOdometer", l => l.BeginOdometer),
        Int("ColEndOdometer", l => l.EndOdometer),
        Int("ColMileage", l => l.Distance),
        new() { Header = Loc.Get("ColDriver"), Text = o => ((LogbookInfo)o).DriverName, Width = 150 },
    ];

    static string Gps(GeoPoint? p) => p is null ? string.Empty : $"{p.Latitude.ToString("0.00000", Loc.Pl)}; {p.Longitude.ToString("0.00000", Loc.Pl)}";

    static readonly IReadOnlyList<TableColumn> EntryColumns =
    [
        new() { Header = Loc.Get("ColAimTrip"), Text = o => ((LogbookEntry)o).AimTrip, Width = 140 },
        new() { Header = Loc.Get("ColDepartureTime"), Text = o => Loc.DateTimeShort(((LogbookEntry)o).BeginTime), SortKey = o => ((LogbookEntry)o).BeginTime, Width = 130,
                ExportValue = o => Loc.Raw(((LogbookEntry)o).BeginTime), ExportKind = ExportColumnKind.DateTime },
        new() { Header = Loc.Get("ColDepartureAddress"), Text = o => ((LogbookEntry)o).BeginAddress, Width = 200, Wrap = true },
        new() { Header = Loc.Get("ColDepartureGps"), Text = o => Gps(((LogbookEntry)o).BeginLocation), Width = 150 },
        new() { Header = Loc.Get("ColDepartureOdometer"), Text = o => Loc.N0(((LogbookEntry)o).BeginOdometer), SortKey = o => ((LogbookEntry)o).BeginOdometer, Width = 110,
                Alignment = TextAlignment.Right, ExportValue = o => Loc.Raw(((LogbookEntry)o).BeginOdometer), ExportKind = ExportColumnKind.WholeNumber },
        new() { Header = Loc.Get("ColArrivalTime"), Text = o => Loc.DateTimeShort(((LogbookEntry)o).EndTime), SortKey = o => ((LogbookEntry)o).EndTime, Width = 130,
                ExportValue = o => Loc.Raw(((LogbookEntry)o).EndTime), ExportKind = ExportColumnKind.DateTime },
        new() { Header = Loc.Get("ColArrivalAddress"), Text = o => ((LogbookEntry)o).EndAddress, Width = 200, Wrap = true },
        new() { Header = Loc.Get("ColArrivalGps"), Text = o => Gps(((LogbookEntry)o).EndLocation), Width = 150 },
        new() { Header = Loc.Get("ColArrivalOdometer"), Text = o => Loc.N0(((LogbookEntry)o).EndOdometer), SortKey = o => ((LogbookEntry)o).EndOdometer, Width = 110,
                Alignment = TextAlignment.Right, ExportValue = o => Loc.Raw(((LogbookEntry)o).EndOdometer), ExportKind = ExportColumnKind.WholeNumber },
        new() { Header = Loc.Get("ColMileage"), Text = o => Loc.N0(((LogbookEntry)o).Distance), SortKey = o => ((LogbookEntry)o).Distance, Width = 90,
                Alignment = TextAlignment.Right, ExportValue = o => Loc.Raw(((LogbookEntry)o).Distance), ExportKind = ExportColumnKind.WholeNumber },
        new() { Header = Loc.Get("ColDriver"), Text = o => ((LogbookEntry)o).DriverName, Width = 150 },
    ];

    readonly LogbooksViewModel _vm = App.Services.GetRequiredService<LogbooksViewModel>();
    bool _init;

    public LogbooksPanel()
    {
        InitializeComponent();
        LogbookTable.SetData(LogbookColumns, Array.Empty<object>());
        EntriesTable.SetData(EntryColumns, Array.Empty<object>());
        _init = true;
        FromPicker.Date = _vm.From;
        ToPicker.Date = _vm.To;
        _init = false;
        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(LogbooksViewModel.Logbooks):
                    LogbookTable.SetData(LogbookColumns, _vm.Logbooks);
                    ExportButton.IsEnabled = _vm.Logbooks.Count > 0;
                    break;
                case nameof(LogbooksViewModel.Entries) when _vm.Entries.Count == 0:
                    EntriesTable.Clear();
                    break;
                case nameof(LogbooksViewModel.Entries):
                    EntriesTable.SetData(EntryColumns, _vm.Entries);
                    break;
                case nameof(LogbooksViewModel.IsBusy):
                    Busy.IsActive = _vm.IsBusy;
                    break;
            }
        };
        LogbookTable.SelectionChanged += async (_, items) =>
        {
            DetailsButton.IsEnabled = items.Count > 0;
            if (items.FirstOrDefault() is LogbookInfo l) await _vm.LoadEntriesAsync(l);
        };
        RegisterPropertyChangedCallback(VisibilityProperty, async (_, _) =>
        {
            if (Visibility == Visibility.Visible && !_vm.IsLoaded) await _vm.LoadAsync();
        });
    }

    async void OnDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_init || FromPicker.Date is not { } from || ToPicker.Date is not { } to) return;
        _vm.From = from.Date;
        _vm.To = to.Date;
        await _vm.LoadAsync();
    }

    async void OnDetailsClick(object sender, RoutedEventArgs e)
    {
        if (LogbookTable.SelectedItem is LogbookInfo l) await _vm.LoadEntriesAsync(l);
    }

    async void OnExportClick(object sender, RoutedEventArgs e) =>
        await App.Services.GetRequiredService<ExportService>().ExportAsync(LogbookTable.ToExport(Loc.Get("SheetLogbooks")), _vm.FileName);
}
