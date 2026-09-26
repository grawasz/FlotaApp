using System.Diagnostics.CodeAnalysis;
using Uno.Resizetizer;

namespace Flota.App;

public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Uno.Extensions APIs are used in a way that is safe for trimming in this template context.")]
    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            // Add navigation support for toolkit controls such as TabBar and NavigationView
            .UseToolkitNavigation()
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                {
                    // Configure log levels for different categories of logging
                    logBuilder
                        .SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                                LogLevel.Information :
                                LogLevel.Warning)

                        // Default filters for core Uno Platform namespaces
                        .CoreLogLevel(LogLevel.Warning);

                    // Uno Platform namespace filter groups
                    // Uncomment individual methods to see more detailed logging
                    //// Generic Xaml events
                    //logBuilder.XamlLogLevel(LogLevel.Debug);
                    //// Layout specific messages
                    //logBuilder.XamlLayoutLogLevel(LogLevel.Debug);
                    //// Storage messages
                    //logBuilder.StorageLogLevel(LogLevel.Debug);
                    //// Binding related messages
                    //logBuilder.XamlBindingLogLevel(LogLevel.Debug);
                    //// Binder memory references tracking
                    //logBuilder.BinderMemoryReferenceLogLevel(LogLevel.Debug);
                    //// DevServer and HotReload related
                    //logBuilder.HotReloadCoreLogLevel(LogLevel.Information);
                    //// Debug JS interop
                    //logBuilder.WebAssemblyLogLevel(LogLevel.Debug);

                }, enableUnoLogging: true)
                .UseConfiguration(configure: configBuilder =>
                    configBuilder
                        .EmbeddedSource<App>()
                        .Section<AppConfig>()
                )
                // Enable localization (see appsettings.json for supported languages)
                .UseLocalization()
                .UseHttp((context, services) => {
#if DEBUG
                // DelegatingHandler will be automatically injected
                services.AddTransient<DelegatingHandler, DebugHttpHandler>();
#endif

})
                .UseAuthentication(auth =>
    auth.AddCustom(custom =>
            custom
                .Login((sp, dispatcher, credentials, cancellationToken) =>
                {
                    // TODO: Write code to process credentials that are passed into the LoginAsync method
                    if (credentials?.TryGetValue(nameof(LoginViewModel.Username), out var username) ?? false &&
                        !username.IsNullOrEmpty())
                    {
                        // Return IDictionary containing any tokens used by service calls or in the app
                        credentials ??= new Dictionary<string, string>();
                        credentials[TokenCacheExtensions.AccessTokenKey] = "SampleToken";
                        credentials[TokenCacheExtensions.RefreshTokenKey] = "RefreshToken";
                        credentials["Expiry"] = DateTime.Now.AddMinutes(5).ToString("g");
                        return ValueTask.FromResult<IDictionary<string, string>?>(credentials);
                    }

                    // Return null/default to fail the LoginAsync method
                    return ValueTask.FromResult<IDictionary<string, string>?>(default);
                })
                .Refresh((sp, tokenDictionary, cancellationToken) =>
                {
                    // TODO: Write code to refresh tokens using the currently stored tokens
                    if ((tokenDictionary?.TryGetValue(TokenCacheExtensions.RefreshTokenKey, out var refreshToken) ?? false) &&
                        !refreshToken.IsNullOrEmpty() &&
                        (tokenDictionary?.TryGetValue("Expiry", out var expiry) ?? false) &&
                        DateTime.TryParse(expiry, out var tokenExpiry) &&
                        tokenExpiry > DateTime.Now)
                    {
                        // Return IDictionary containing any tokens used by service calls or in the app
                        tokenDictionary ??= new Dictionary<string, string>();
                        tokenDictionary[TokenCacheExtensions.AccessTokenKey] = "NewSampleToken";
                        tokenDictionary["Expiry"] = DateTime.Now.AddMinutes(5).ToString("g");
                        return ValueTask.FromResult<IDictionary<string, string>?>(tokenDictionary);
                    }

                    // Return null/default to fail the Refresh method
                    return ValueTask.FromResult<IDictionary<string, string>?>(default);
                }), name: "CustomAuth")
                )
                .ConfigureServices((context, services) =>
                {
                    // TODO: Register your services
                    //services.AddSingleton<IMyService, MyService>();
                })
                .UseNavigation(RegisterRoutes)
            );
        MainWindow = builder.Window;

        #if DEBUG
        MainWindow.UseStudio();
#endif
                MainWindow.SetWindowIcon();

        async Task InitialNavigate(IServiceProvider services, INavigator navigator)
        {
            var auth = services.GetRequiredService<IAuthenticationService>();
            var authenticated = await auth.RefreshAsync();
            if (authenticated)
            {
                await navigator.NavigateViewModelAsync<MainViewModel>(this, qualifier: Qualifiers.ClearBackStack);
            }
            else
            {
                await navigator.NavigateViewModelAsync<LoginViewModel>(this, qualifier: Qualifiers.ClearBackStack);
            }
        }
        Host = await MainWindow.InitializeNavigationAsync(
            () => Task.FromResult(builder.Build()),
            initialNavigate: InitialNavigate);
    }

    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap<LoginPage, LoginViewModel>(),
            new ViewMap<MainPage, MainViewModel>()
        );

        routes.Register(
            new RouteMap("Login", View: views.FindByViewModel<LoginViewModel>()),
            new RouteMap("Main", View: views.FindByViewModel<MainViewModel>(), IsDefault:true)
        );
    }
}
