using Flota.Core.Contracts;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Flota.Client;

/// <summary>Stan połączenia z hubem pozycji.</summary>
public enum LiveConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
}

/// <summary>Strumień zmian pozycji floty.</summary>
public interface IFleetLiveFeed
{
    event EventHandler<IReadOnlyList<PositionChanged>>? PositionsChanged;
    event EventHandler<AddressResolved>? AddressResolved;
    event EventHandler<LiveConnectionState>? StateChanged;

    LiveConnectionState State { get; }

    Task StartAsync(CancellationToken ct);

    Task StopAsync();
}

/// <summary>Klient huba pozycji floty (SignalR, automatyczne wznawianie — D10).</summary>
public sealed class FleetHubClient(FlotaApiClient api) : IFleetLiveFeed, IAsyncDisposable
{
    HubConnection? _connection;

    public event EventHandler<IReadOnlyList<PositionChanged>>? PositionsChanged;
    public event EventHandler<AddressResolved>? AddressResolved;
    public event EventHandler<LiveConnectionState>? StateChanged;

    public LiveConnectionState State { get; private set; } = LiveConnectionState.Disconnected;

    public async Task StartAsync(CancellationToken ct)
    {
        if (_connection is not null) return;
        var baseUri = api.BaseAddress ?? throw new InvalidOperationException("Brak adresu Flota.Api.");
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(baseUri, "hubs/fleet"), o => o.AccessTokenProvider = async () =>
            {
                if (api.AccessToken is null) await api.RefreshAsync(CancellationToken.None).ConfigureAwait(false);
                return api.AccessToken;
            })
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)])
            .AddJsonProtocol(o => o.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, FlotaJsonContext.Default))
            .Build();

        connection.On<IReadOnlyList<PositionChanged>>("PositionsChanged", p => PositionsChanged?.Invoke(this, p));
        connection.On<AddressResolved>("AddressResolved", a => AddressResolved?.Invoke(this, a));
        connection.On<string>("ConnectionState", async state =>
        {
            // Token UniAVL wygasł po stronie serwera — odświeżenie wymaga ponownego logowania
            if (state == "unauthorized") await api.RefreshAsync(CancellationToken.None).ConfigureAwait(false);
        });
        connection.Reconnecting += _ => SetState(LiveConnectionState.Reconnecting);
        connection.Reconnected += _ => SetState(LiveConnectionState.Connected);
        connection.Closed += async _ =>
        {
            await SetState(LiveConnectionState.Disconnected).ConfigureAwait(false);
            // Ponowna próba po wyczerpaniu automatycznych wznowień
            await Task.Delay(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            if (_connection == connection) await TryStartAsync(connection, CancellationToken.None).ConfigureAwait(false);
        };

        _connection = connection;
        await TryStartAsync(connection, ct).ConfigureAwait(false);
    }

    async Task TryStartAsync(HubConnection connection, CancellationToken ct)
    {
        await SetState(LiveConnectionState.Connecting).ConfigureAwait(false);
        try
        {
            await connection.StartAsync(ct).ConfigureAwait(false);
            await SetState(LiveConnectionState.Connected).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TimeoutException)
        {
            await SetState(LiveConnectionState.Disconnected).ConfigureAwait(false);
        }
    }

    Task SetState(LiveConnectionState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        var c = _connection;
        _connection = null;
        if (c is not null) await c.DisposeAsync().ConfigureAwait(false);
        await SetState(LiveConnectionState.Disconnected).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
