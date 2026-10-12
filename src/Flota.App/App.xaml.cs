using System.Diagnostics.CodeAnalysis;
using Flota.App.Services;
using Flota.Client;
using Uno.Resizetizer;

namespace Flota.App;

public partial class App : Application
{
    public App()
    {
        if (ThemeSettings.ToApplicationTheme(ThemeSettings.Load()) is { } theme) RequestedTheme = theme;
        InitializeComponent();
        UnhandledException += (_, e) => WriteCrashLog(e.Exception);
    }

    /// <summary>Zapis nieobsłużonego wyjątku do pliku diagnostycznego (katalog tymczasowy).</summary>
    static void WriteCrashLog(Exception? ex)
    {
        try
        {
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "flota-app-error.log"), $"{DateTime.Now:O}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
    }

    public static Window MainAppWindow { get; private set; } = null!;

    /// <summary>Adres strony w przeglądarce (WebAssembly); <c>null</c> na pozostałych platformach.</summary>
    public static string? BrowserOrigin { get; set; }
    public static IServiceProvider Services { get; private set; } = null!;

    Frame? _frame;
    DispatcherQueueTimer? _statusTimer;

    [SuppressMessage("Trimming", "IL2026", Justification = "Uno.Extensions Hosting — konfiguracja bez refleksji w kodzie aplikacji.")]
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Launch(args);
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            throw;
        }
    }

    [SuppressMessage("Trimming", "IL2026", Justification = "Uno.Extensions Hosting — konfiguracja bez refleksji w kodzie aplikacji.")]
    void Launch(LaunchActivatedEventArgs args)
    {
        Pomiar.Mark("app.launch");
        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging((context, log) => log
                    .SetMinimumLevel(context.HostingEnvironment.IsDevelopment() ? LogLevel.Information : LogLevel.Warning)
                    .CoreLogLevel(LogLevel.Warning), enableUnoLogging: true)
                .UseConfiguration(configure: c => c.EmbeddedSource<App>().Section<AppConfig>())
                .UseLocalization()
                .ConfigureServices((context, services) => ConfigureServices(context, services)));

        MainAppWindow = builder.Window;
#if DEBUG
        MainAppWindow.UseStudio();
#endif
        MainAppWindow.SetWindowIcon();
        MainAppWindow.Title = "Flota";
        var host = builder.Build();
        Services = host.Services;
        var localizer = Services.GetRequiredService<IStringLocalizer>();
        Loc.Initialize(key => localizer[key] is { ResourceNotFound: false } s ? s.Value : null);
        AttachFleet(Services.GetRequiredService<FleetViewModel>());
        Services.GetRequiredService<FlotaApiClient>().SessionExpired += (_, _) =>
            MainAppWindow.DispatcherQueue.TryEnqueue(async () => await SignOutAsync(expired: true));

        _frame = new Frame();
        MainAppWindow.Content = _frame;
        MainAppWindow.Activate();
#if DEBUG
        DevSnapshot.Start(MainAppWindow);
#endif

        Pomiar.Mark("app.activated");
        Pomiar.MarkNextFrame("app.rendered");
        var tokens = Services.GetRequiredService<ITokenStore>();
        if (tokens.Current is not null) ShowShell();
        else ShowLogin();
    }

    static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // Przeglądarka: tokeny wyłącznie w pamięci (bez localStorage); trwała sesja — ciasteczko HttpOnly (T9.2)
        if (OperatingSystem.IsBrowser()) services.AddSingleton<ITokenStore, InMemoryTokenStore>();
        else services.AddSingleton<ITokenStore, SecureTokenStore>();
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IOptions<AppConfig>>().Value;
            var baseUrl = config.ApiBaseUrl;
#if __ANDROID__
            var emulator = Android.OS.Build.Fingerprint?.Contains("generic", StringComparison.OrdinalIgnoreCase) == true
                || Android.OS.Build.Hardware is "ranchu" or "goldfish"
                || Android.OS.Build.Model?.Contains("sdk_gphone", StringComparison.OrdinalIgnoreCase) == true;
            if (emulator
                && !string.IsNullOrEmpty(config.ApiBaseUrlAndroidEmulator))
                baseUrl = config.ApiBaseUrlAndroidEmulator;
#endif
            if (string.IsNullOrWhiteSpace(baseUrl) && BrowserOrigin is { Length: > 0 } origin)
                baseUrl = origin.TrimEnd('/') + "/";
            var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            if (!string.IsNullOrWhiteSpace(baseUrl)) http.BaseAddress = new Uri(baseUrl);
            return new FlotaApiClient(http, sp.GetRequiredService<ITokenStore>());
        });
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<FleetHubClient>();
        services.AddSingleton<IFleetLiveFeed>(sp => sp.GetRequiredService<FleetHubClient>());
        services.AddSingleton<ISnapshotStore, SnapshotCache>();
        services.AddSingleton<Notifier>();
        services.AddSingleton<MapImages>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<FleetViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<RouteReportViewModel>();
        services.AddTransient<FleetReportViewModel>();
        services.AddTransient<EcoDrivingViewModel>();
        services.AddTransient<LogbooksViewModel>();
        services.AddTransient<DriversViewModel>();
        services.AddTransient<EquipmentViewModel>();
        services.AddTransient<EventsViewModel>();
    }

    /// <summary>Zdarzenia huba w wątku interfejsu; przeliczenie statusów co minutę (próg nieaktualności).</summary>
    void AttachFleet(FleetViewModel fleet)
    {
        if (SynchronizationContext.Current is { } ui) fleet.AttachUiContext(ui);
        _statusTimer = MainAppWindow.DispatcherQueue.CreateTimer();
        _statusTimer.Interval = TimeSpan.FromMinutes(1);
        _statusTimer.Tick += (_, _) => fleet.RefreshStatuses();
        _statusTimer.Start();
    }

    public void ShowLogin()
    {
        _frame?.Navigate(typeof(Presentation.LoginPage));
        _frame?.BackStack.Clear();
    }

    /// <summary>
    /// Strona główna. Wczytanie stanu floty startuje przed budową strony — żądanie <c>/fleet/snapshot</c> jest wysyłane,
    /// zanim interfejs zajmie wątek (WebAssembly: jeden wątek); strona oczekuje na przekazane zadanie.
    /// </summary>
    public void ShowShell()
    {
        Pomiar.Mark("shell.start");
        var load = Services.GetRequiredService<FleetViewModel>().LoadAsync();
        _frame?.Navigate(typeof(Presentation.ShellPage), load);
        _frame?.BackStack.Clear();
    }

    /// <summary>Wylogowanie: unieważnienie sesji, usunięcie tokenów i stanu lokalnego.</summary>
    public async Task SignOutAsync(bool expired = false)
    {
        var api = Services.GetRequiredService<FlotaApiClient>();
        var fleet = Services.GetRequiredService<FleetViewModel>();
        if (!expired) await api.LogoutAsync(CancellationToken.None);
        else await Services.GetRequiredService<ITokenStore>().ClearAsync();
        await fleet.ClearAsync();
        Services.GetRequiredService<RouteReportViewModel>().Reset();
        ShowLogin();
        if (expired) Services.GetRequiredService<Notifier>().Info(Loc.Get("SessionExpired"));
    }

    public static new App Current => (App)Application.Current;
}
