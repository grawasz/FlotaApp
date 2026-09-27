using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Flota.Client;
using Flota.Core.Contracts;

namespace Flota.Presentation.Reports;

/// <summary>Raport floty (odpowiednik panelu „Raport floty”): podsumowanie na obiekt i szczegóły dzienne.</summary>
public sealed partial class FleetReportViewModel : BusyViewModel
{
    readonly FlotaApiClient _api;

    public FleetReportViewModel(FlotaApiClient api, Notifier notifier, TimeProvider time) : base(notifier)
    {
        _api = api;
        // Domyślnie: wczoraj — dziś (jak w aplikacji źródłowej)
        var today = time.GetLocalNow().Date;
        From = today.AddDays(-1);
        To = today;
    }

    [ObservableProperty] public partial DateTime From { get; set; }
    [ObservableProperty] public partial DateTime To { get; set; }

    public IReadOnlyList<FleetReportRow> Summary { get; private set; } = [];
    public IReadOnlyList<FleetReportRow> Details { get; private set; } = [];

    /// <summary>
    /// Kolumna wartości widoczna, gdy przynajmniej jeden wiersz ma wartość; przy braku wierszy — widoczna
    /// (odpowiednik warunków widoczności kolumn w aplikacji źródłowej).
    /// </summary>
    public static bool IsColumnVisible<T>(IReadOnlyList<FleetReportRow> rows, Func<FleetReportRow, T?> value) =>
        rows.Count == 0 || rows.Any(r => value(r) is { } v && v is not "");

    public async Task GenerateAsync(IReadOnlyList<int> objectIds)
    {
        if (!RequireObjects(objectIds)) return;
        await RunAsync(async () =>
        {
            var report = await _api.GetFleetReportAsync(new FleetReportRequest(objectIds, From.Date, To.Date), CancellationToken.None);
            Summary = report.Summary;
            Details = report.Details;
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(Details));
        });
    }

    public string FileBase => $"{Loc.Get("FileFleetReport")} {Stamp(From)} {Stamp(To)}";

    internal static string Stamp(DateTime d) => d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
}

/// <summary>Ranking Eco-Driving: ocena stylu jazdy obiektów w okresie (pełne dni).</summary>
public sealed partial class EcoDrivingViewModel : BusyViewModel
{
    readonly FlotaApiClient _api;

    public EcoDrivingViewModel(FlotaApiClient api, Notifier notifier, TimeProvider time) : base(notifier)
    {
        _api = api;
        var today = time.GetLocalNow().Date;
        From = today.AddDays(-7);
        To = today;
    }

    [ObservableProperty] public partial DateTime From { get; set; }
    [ObservableProperty] public partial DateTime To { get; set; }

    public IReadOnlyList<EcoDrivingRow> Rows { get; private set; } = [];

    /// <summary>Okres zapytania: od początku dnia „od” do końca dnia „do”.</summary>
    public EcoDrivingRequest Request(IReadOnlyList<int> objectIds) =>
        new(objectIds, From.Date, To.Date.AddDays(1).AddSeconds(-1));

    public async Task GenerateAsync(IReadOnlyList<int> objectIds)
    {
        if (!RequireObjects(objectIds)) return;
        await RunAsync(async () =>
        {
            Rows = await _api.GetEcoDrivingAsync(Request(objectIds), CancellationToken.None);
            OnPropertyChanged(nameof(Rows));
        });
    }

    public string FileName => $"{Loc.Get("FileEcoDriving")} {FleetReportViewModel.Stamp(From)} {FleetReportViewModel.Stamp(To)}.xlsx";
}

/// <summary>Karty drogowe: lista kart w okresie i pozycje wybranej karty.</summary>
public sealed partial class LogbooksViewModel : BusyViewModel
{
    readonly FlotaApiClient _api;

    public LogbooksViewModel(FlotaApiClient api, Notifier notifier, TimeProvider time) : base(notifier)
    {
        _api = api;
        var today = time.GetLocalNow().Date;
        From = today.AddDays(-1);
        To = today;
    }

    [ObservableProperty] public partial DateTime From { get; set; }
    [ObservableProperty] public partial DateTime To { get; set; }

    public IReadOnlyList<LogbookInfo> Logbooks { get; private set; } = [];
    public IReadOnlyList<LogbookEntry> Entries { get; private set; } = [];

    /// <summary>Lista wczytana co najmniej raz (panel wczytuje dane przy pierwszym wyświetleniu).</summary>
    public bool IsLoaded { get; private set; }

    public async Task LoadAsync()
    {
        IsLoaded = true;
        await RunAsync(async () =>
        {
            Logbooks = await _api.GetLogbooksAsync(From.Date, To.Date, CancellationToken.None);
            Entries = [];
            OnPropertyChanged(nameof(Logbooks));
            OnPropertyChanged(nameof(Entries));
        });
    }

    public async Task LoadEntriesAsync(LogbookInfo logbook)
    {
        await RunAsync(async () =>
        {
            Entries = await _api.GetLogbookEntriesAsync(logbook.Id, CancellationToken.None);
            OnPropertyChanged(nameof(Entries));
        });
    }

    public string FileName => $"{Loc.Get("FileLogbooks")} {FleetReportViewModel.Stamp(From)} {FleetReportViewModel.Stamp(To)}.xlsx";
}
