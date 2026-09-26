using System.Diagnostics.CodeAnalysis;
using Flota.App.Presentation.Fleet;
using Flota.App.Presentation.Reports;
using Flota.App.Services;
using Flota.Client;
using Uno.Resizetizer;

namespace Flota.App;

public partial class App : Application
{
    public App()
    {
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
    public static IServiceProvider Services { get; private set; } = null!;

    Frame? _frame;

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
        Loc.Initialize(Services.GetRequiredService<IStringLocalizer>());
        Services.GetRequiredService<FleetViewModel>().AttachDispatcher(DispatcherQueue.GetForCurrentThread());
        Services.GetRequiredService<FlotaApiClient>().SessionExpired += (_, _) =>
            MainAppWindow.DispatcherQueue.TryEnqueue(async () => await SignOutAsync(expired: true));

        _frame = new Frame();
        MainAppWindow.Content = _frame;
        MainAppWindow.Activate();
#if DEBUG
        DevSnapshot.Start(MainAppWindow);
#endif

        var tokens = Services.GetRequiredService<ITokenStore>();
        if (tokens.Current is not null) ShowShell();
        else ShowLogin();
    }

    static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<ITokenStore, SecureTokenStore>();
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IOptions<AppConfig>>().Value;
            var baseUrl = config.ApiBaseUrl;
#if __ANDROID__
            if (Android.OS.Build.Fingerprint?.Contains("generic", StringComparison.OrdinalIgnoreCase) == true
                && !string.IsNullOrEmpty(config.ApiBaseUrlAndroidEmulator))
                baseUrl = config.ApiBaseUrlAndroidEmulator;
#endif
            var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            if (!string.IsNullOrWhiteSpace(baseUrl)) http.BaseAddress = new Uri(baseUrl);
            return new FlotaApiClient(http, sp.GetRequiredService<ITokenStore>());
        });
        services.AddSingleton<FleetHubClient>();
        services.AddSingleton<SnapshotCache>();
        services.AddSingleton<Notifier>();
        services.AddSingleton<MapImages>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<FleetViewModel>();
        services.AddSingleton<Presentation.ShellViewModel>();
        services.AddSingleton<RouteReportViewModel>();
    }

    public void ShowLogin()
    {
        _frame?.Navigate(typeof(Presentation.LoginPage));
        _frame?.BackStack.Clear();
    }

    public void ShowShell()
    {
        _frame?.Navigate(typeof(Presentation.ShellPage));
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
