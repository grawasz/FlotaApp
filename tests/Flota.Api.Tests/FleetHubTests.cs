using System.Threading.Channels;
using Flota.Core.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Flota.Api.Tests;

/// <summary>Hub pozycji <c>/hubs/fleet</c> (T2.8): odpytywanie symulatora UniAVL i rozsyłanie wyłącznie zmienionych pozycji.</summary>
public sealed class FleetHubTests(SimulatorFixture sim) : IClassFixture<SimulatorFixture>
{
    static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PositionsChanged_FirstAllThenOnlyChanged()
    {
        using var api = await sim.LoginAsync();
        var updates = Channel.CreateUnbounded<IReadOnlyList<PositionChanged>>();
        var server = sim.Api.Server;
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "hubs/fleet"), o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => server.CreateHandler();
                o.AccessTokenProvider = () => Task.FromResult(api.AccessToken);
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, FlotaJsonContext.Default))
            .Build();
        connection.On<IReadOnlyList<PositionChanged>>("PositionsChanged", p => updates.Writer.TryWrite(p));
        await connection.StartAsync(Ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(Wait);

        // Pierwsze odpytanie: wszystkie obiekty z urządzeniem (11 z 12)
        var first = await updates.Reader.ReadAsync(timeout.Token);
        Assert.Equal(11, first.Count);
        Assert.DoesNotContain(first, p => p.ObjectId == 112);

        // Minuta później: nowe wiadomości wyłącznie od obiektów jeżdżących i nadających bez GPS;
        // obiekty na postoju nadają co 5 minut, obiekt bez łączności nie nadaje
        sim.Clock.Local = SimulatorFixture.Now.AddMinutes(1);
        var next = await updates.Reader.ReadAsync(timeout.Token);
        Assert.Equal([101, 102, 103, 105, 106, 107, 110, 111], next.Select(p => p.ObjectId).Order());
        Assert.All(next, p => Assert.Equal(SimulatorFixture.Now.AddMinutes(1), p.Position.Time));
    }
}
