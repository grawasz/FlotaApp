using System.Net;
using Flota.Client;
using Flota.Core.Contracts;
using Flota.Presentation.Dictionaries;
using Flota.Presentation.Reports;
using Microsoft.Extensions.Time.Testing;

namespace Flota.Presentation.Tests;

/// <summary>Modele widoku paneli: raport floty, Eco-Driving, karty drogowe, kierowcy, wyposażenie, zdarzenia.</summary>
public sealed class PanelViewModelTests : IDisposable
{
    readonly FakeApi _api = new();
    readonly Notifier _notifier = new();
    readonly List<Notice> _notices = [];
    readonly FakeTimeProvider _clock = new(new DateTimeOffset(Data.Now, TimeSpan.Zero));
    readonly FlotaApiClient _client;

    public PanelViewModelTests()
    {
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        _notifier.Raised += (_, n) => _notices.Add(n);
        _client = _api.Client(Data.Tokens);
    }

    public void Dispose()
    {
        _client.Dispose();
        _api.Dispose();
    }

    static FleetReportRow Row(int id, DateTime? date = null, double? fuel = null, string? doors = null) =>
        new() { ObjectId = id, ObjectName = $"Obiekt {id}", Date = date, FuelUsed = fuel, Doors = doors };

    // ---------------- Raport floty ----------------

    [Fact]
    public void FleetReport_DefaultPeriod_YesterdayToday()
    {
        var vm = new FleetReportViewModel(_client, _notifier, _clock);
        Assert.Equal(Data.Now.Date.AddDays(-1), vm.From);
        Assert.Equal(Data.Now.Date, vm.To);
        Assert.Equal("FileFleetReport 20260926 20260927", vm.FileBase);
    }

    [Fact]
    public async Task FleetReport_WithoutObjects_AsksToPick()
    {
        var vm = new FleetReportViewModel(_client, _notifier, _clock);
        await vm.GenerateAsync([]);
        Assert.Empty(_api.Requests);
        Assert.Equal("PickObjectsFirst", Assert.Single(_notices).Message);
    }

    [Fact]
    public async Task FleetReport_Generate()
    {
        _api.On(HttpMethod.Post, "reports/fleet", new FleetReportResponse([Row(1), Row(2)], [Row(1, Data.Now.Date)]), FlotaJsonContext.Default.FleetReportResponse);
        var vm = new FleetReportViewModel(_client, _notifier, _clock);
        var busy = new List<bool>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsBusy)) busy.Add(vm.IsBusy);
        };

        await vm.GenerateAsync([1, 2]);

        Assert.Equal(2, vm.Summary.Count);
        Assert.Single(vm.Details);
        Assert.Equal([true, false], busy);
        Assert.Contains("\"objectIds\":[1,2]", _api.Bodies.Last(), StringComparison.Ordinal);
        Assert.Contains("\"from\":\"2026-09-26T00:00:00\"", _api.Bodies.Last(), StringComparison.Ordinal);
    }

    [Fact]
    public void FleetReport_ColumnVisibleOnlyWithValues()
    {
        IReadOnlyList<FleetReportRow> rows = [Row(1), Row(2, fuel: 3.5)];
        Assert.True(FleetReportViewModel.IsColumnVisible(rows, r => r.FuelUsed));
        Assert.False(FleetReportViewModel.IsColumnVisible(rows, r => r.Odometer));
        Assert.False(FleetReportViewModel.IsColumnVisible([Row(1, doors: "")], r => r.Doors));
        Assert.True(FleetReportViewModel.IsColumnVisible([Row(1, doors: "00:01:00")], r => r.Doors));
        // Brak wierszy — nagłówki wszystkich kolumn
        Assert.True(FleetReportViewModel.IsColumnVisible([], r => r.Odometer));
    }

    [Fact]
    public async Task FleetReport_ServerError_Notifies()
    {
        _api.On(HttpMethod.Post, "reports/fleet", HttpStatusCode.InternalServerError);
        var vm = new FleetReportViewModel(_client, _notifier, _clock);
        await vm.GenerateAsync([1]);
        Assert.Equal(NoticeSeverity.Error, Assert.Single(_notices).Severity);
        Assert.False(vm.IsBusy);
        Assert.Empty(vm.Summary);
    }

    // ---------------- Eco-Driving ----------------

    [Fact]
    public async Task EcoDriving_FullDaysAndRanking()
    {
        _api.On(HttpMethod.Post, "reports/eco-driving", (IReadOnlyList<EcoDrivingRow>)[new EcoDrivingRow { Position = 1, ObjectId = 1, Score = 90 }], FlotaJsonContext.Default.IReadOnlyListEcoDrivingRow);
        var vm = new EcoDrivingViewModel(_client, _notifier, _clock);
        Assert.Equal(Data.Now.Date.AddDays(-7), vm.From);

        var request = vm.Request([1]);
        Assert.Equal(Data.Now.Date.AddDays(-7), request.From);
        Assert.Equal(Data.Now.Date.AddDays(1).AddSeconds(-1), request.To);

        await vm.GenerateAsync([1]);
        Assert.Equal(90, Assert.Single(vm.Rows).Score);
        Assert.Equal("FileEcoDriving 20260920 20260927.xlsx", vm.FileName);
    }

    // ---------------- Karty drogowe ----------------

    [Fact]
    public async Task Logbooks_LoadListThenEntries()
    {
        var logbook = new LogbookInfo { Id = 7, Date = Data.Now.Date };
        // Trasy sprawdzane od ostatnio dodanej — ścieżka szczegółowa po ogólnej
        _api.On(HttpMethod.Get, "logbooks", (IReadOnlyList<LogbookInfo>)[logbook], FlotaJsonContext.Default.IReadOnlyListLogbookInfo)
            .On(HttpMethod.Get, "logbooks/7", (IReadOnlyList<LogbookEntry>)[new LogbookEntry { Id = 70, AimTrip = "Dostawa" }], FlotaJsonContext.Default.IReadOnlyListLogbookEntry);
        var vm = new LogbooksViewModel(_client, _notifier, _clock);
        Assert.False(vm.IsLoaded);

        await vm.LoadAsync();
        Assert.True(vm.IsLoaded);
        Assert.Single(vm.Logbooks);
        Assert.Contains("from=2026-09-26T00%3A00%3A00", _api.Requests.Last().RequestUri!.Query, StringComparison.Ordinal);

        await vm.LoadEntriesAsync(logbook);
        Assert.Equal("Dostawa", Assert.Single(vm.Entries).AimTrip);

        // Ponowne wczytanie listy czyści pozycje
        await vm.LoadAsync();
        Assert.Empty(vm.Entries);
    }

    // ---------------- Słowniki ----------------

    [Fact]
    public async Task Drivers_LoadedOnce_RetryAfterFailure()
    {
        _api.On(HttpMethod.Get, "dictionaries/drivers", HttpStatusCode.BadGateway);
        var vm = new DriversViewModel(_client, _notifier);

        await vm.EnsureLoadedAsync();
        Assert.Empty(vm.Drivers);

        _api.On(HttpMethod.Get, "dictionaries/drivers", (IReadOnlyList<DriverInfo>)[new DriverInfo(1, "Anna", "Nowak", "anowak", true)], FlotaJsonContext.Default.IReadOnlyListDriverInfo);
        await vm.EnsureLoadedAsync();
        await vm.EnsureLoadedAsync();

        Assert.Single(vm.Drivers);
        Assert.Equal(2, _api.Count("dictionaries/drivers"));
    }

    [Fact]
    public async Task Equipment_RefreshBypassesServerCache()
    {
        _api.On(HttpMethod.Get, "dictionaries/equipment", (IReadOnlyList<EquipmentInfo>)[new EquipmentInfo { Id = 1 }], FlotaJsonContext.Default.IReadOnlyListEquipmentInfo);
        var vm = new EquipmentViewModel(_client, _notifier, _clock);

        await vm.EnsureLoadedAsync();
        await vm.EnsureLoadedAsync();
        await vm.RefreshAsync();

        Assert.Equal(["?refresh=false", "?refresh=true"], _api.Requests.Select(r => r.RequestUri!.Query));
        Assert.Single(vm.Items);
        Assert.Equal("FileEquipment 20260927_1200.xlsx", vm.FileName);
    }

    [Fact]
    public async Task Events_SplitTemplatesAndReminders()
    {
        _api.On(HttpMethod.Post, "dictionaries/event-templates", (IReadOnlyList<EventTemplateInfo>)
        [
            new EventTemplateInfo { Id = 1, Name = "Strefa" },
            new EventTemplateInfo { Id = 2, Name = "Przegląd", IsReminder = true },
            new EventTemplateInfo { Id = 3, Name = "Prędkość" },
        ], FlotaJsonContext.Default.IReadOnlyListEventTemplateInfo);
        var vm = new EventsViewModel(_client, _notifier);

        await vm.LoadAsync([]);
        Assert.Empty(_api.Requests);

        await vm.LoadAsync([5]);
        Assert.Equal([1, 3], vm.Templates.Select(t => t.Id));
        Assert.Equal([2], vm.Reminders.Select(t => t.Id));
    }

    [Fact]
    public async Task SessionExpired_NoErrorNotice()
    {
        _api.On(HttpMethod.Get, "dictionaries/drivers", HttpStatusCode.Unauthorized)
            .On(HttpMethod.Post, "auth/refresh", HttpStatusCode.Unauthorized);
        var expired = false;
        _client.SessionExpired += (_, _) => expired = true;
        var vm = new DriversViewModel(_client, _notifier);

        await vm.EnsureLoadedAsync();

        Assert.True(expired);
        Assert.Empty(_notices);
        Assert.False(vm.IsBusy);
    }
}

/// <summary>Powłoka i komunikaty.</summary>
public sealed class ShellAndNotifierTests
{
    [Fact]
    public void Shell_PortraitShowsOnlyMap()
    {
        var shell = new ShellViewModel();
        shell.Initialize(portrait: true);
        Assert.False(shell.ShowObjects);
        Assert.False(shell.ShowDetails);
        Assert.True(shell.ShowMap);
        Assert.False(shell.AnyLeft);

        shell.Initialize(portrait: false);
        Assert.True(shell.AnyLeft);
        Assert.False(shell.AnyRight);
        shell.ShowRouteReport = true;
        Assert.True(shell.AnyRight);
    }

    [Theory]
    [InlineData(0, "Nie można połączyć")]
    [InlineData(1, "ErrorNoConnection")]
    [InlineData(2, "ErrorTimeout")]
    [InlineData(3, "ErrorGeneral")]
    public void Notifier_ErrorMessageWithoutTechnicalDetails(int kind, string expected)
    {
        Exception ex = kind switch
        {
            0 => new FlotaApiException("Nie można połączyć"),
            1 => new HttpRequestException("socket 10061"),
            2 => new TaskCanceledException(),
            _ => new InvalidOperationException("stos wywołań"),
        };
        var notifier = new Notifier();
        Notice? notice = null;
        notifier.Raised += (_, n) => notice = n;

        notifier.Error(ex);

        Assert.Equal(NoticeSeverity.Error, notice!.Severity);
        Assert.Equal(expected, notice.Message);
    }

    [Fact]
    public void Loc_FormatsPolishValues()
    {
        Assert.Equal("1 234", Loc.N0(1234).Replace(' ', ' '));
        Assert.Equal("3,50", Loc.N2(3.5));
        Assert.Equal("2026-09-27 12:00", Loc.DateTimeShort(Data.Now));
        Assert.Equal("01:02:03", Loc.Duration(new TimeSpan(1, 2, 3)));
        Assert.Equal("1.02:00:00", Loc.Duration(new TimeSpan(1, 2, 0, 0)));
        Assert.Equal(string.Empty, Loc.N1(null));
        Assert.Equal("1.5", Loc.Raw(1.5));
        Assert.Equal("BrakKlucza", Loc.Get("BrakKlucza"));
    }
}

/// <summary>Wybór motywu (T7.2).</summary>
public sealed class ThemeChoiceTests
{
    [Theory]
    [InlineData("Dark", ThemeChoice.Dark)]
    [InlineData("System", ThemeChoice.System)]
    [InlineData("Light", ThemeChoice.Light)]
    [InlineData(null, ThemeChoice.Light)]
    [InlineData("", ThemeChoice.Light)]
    [InlineData("dark", ThemeChoice.Light)]
    [InlineData("7", ThemeChoice.Light)]
    public void Parse_UnknownValue_DefaultsToLight(string? stored, ThemeChoice expected) => Assert.Equal(expected, ThemeChoices.Parse(stored));

    [Fact]
    public void TextKeys_InDisplayOrder()
    {
        Assert.Equal([ThemeChoice.Light, ThemeChoice.Dark, ThemeChoice.System], ThemeChoices.All);
        Assert.Equal("ThemeSystem", ThemeChoices.TextKey(ThemeChoice.System));
    }
}
