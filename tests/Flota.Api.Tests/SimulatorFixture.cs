using Flota.Api.Infrastructure.UniAvl;
using Flota.Client;
using Flota.UniAvlSim;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Flota.Api.Tests;

/// <summary>Zegar testowy: czas lokalny ustawiany ręcznie (strefa UTC).</summary>
sealed class TestClock(DateTime local) : TimeProvider
{
    public DateTime Local { get; set; } = local;

    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Local, DateTimeKind.Utc));

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>
/// Flota.Api połączone z symulatorem UniAVL (<c>tools/Flota.UniAvlSim</c>) na serwerach testowych w pamięci.
/// Czas symulatora zatrzymany na <see cref="Now"/>; Targeo niedostępne (adresy puste).
/// </summary>
public sealed class SimulatorFixture : IAsyncLifetime
{
    public static readonly DateTime Now = new(2026, 9, 27, 13, 0, 0, DateTimeKind.Unspecified);

    readonly string _dir = Path.Combine(Path.GetTempPath(), "flota-api-sim-tests-" + Guid.NewGuid().ToString("N"));
    WebApplication? _sim;

    internal TestClock Clock { get; } = new(Now);

    public WebApplicationFactory<Program> Api { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _sim = SimApp.Create([], b =>
        {
            b.WebHost.UseTestServer();
            b.Services.AddSingleton<TimeProvider>(Clock);
        });
        await _sim.StartAsync();
        var simServer = _sim.GetTestServer();

        Directory.CreateDirectory(_dir);
        Api = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseEnvironment("Testing")
            .UseSetting("UniAvl:BaseUrl", "http://uniavl-sim/AVL/")
            .UseSetting("ConnectionStrings:Flota", $"Data Source={Path.Combine(_dir, "flota.db")};Pooling=False")
            .UseSetting("DataProtection:KeysPath", Path.Combine(_dir, "keys"))
            .UseSetting("Live:PollInterval", "00:00:00.200")
            .UseSetting("Reports:ChartPoints", "200")
            .ConfigureTestServices(s => s.AddHttpClient<UniAvlClient>().ConfigurePrimaryHttpMessageHandler(simServer.CreateHandler)));
    }

    /// <summary>Klient Flota.Api (<see cref="FlotaApiClient"/>) zalogowany kontem symulatora.</summary>
    public async Task<FlotaApiClient> LoginAsync()
    {
        var client = new FlotaApiClient(Api.CreateClient(), new InMemoryTokenStore());
        _ = await client.LoginAsync("demo", "demo", TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("Logowanie do symulatora nie powiodło się.");
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        await Api.DisposeAsync();
        if (_sim is not null) await _sim.DisposeAsync();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }
}
