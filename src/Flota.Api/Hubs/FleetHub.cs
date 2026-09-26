using System.Collections.Concurrent;
using Flota.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Flota.Api.Hubs;

/// <summary>
/// Hub pozycji floty (<c>/hubs/fleet</c>). Zdarzenia: <see cref="PositionsChangedMethod"/> (tylko zmienione obiekty),
/// <see cref="AddressResolvedMethod"/>, <see cref="ConnectionStateMethod"/>.
/// </summary>
[Authorize]
public sealed class FleetHub(LiveSessionRegistry registry) : Hub
{
    public const string Path = "/hubs/fleet";
    public const string PositionsChangedMethod = "PositionsChanged";
    public const string AddressResolvedMethod = "AddressResolved";
    public const string ConnectionStateMethod = "ConnectionState";

    public static string Group(Guid sessionId) => $"s:{sessionId:N}";

    public override async Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.User?.FindFirst(TokenService.SessionClaim)?.Value, out var sid))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, Group(sid));
            registry.Connect(sid, Context.ConnectionId);
        }
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        registry.Disconnect(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

/// <summary>Sesje z aktywnym połączeniem huba (dla nich wykonywane jest odpytywanie pozycji).</summary>
public sealed class LiveSessionRegistry
{
    readonly ConcurrentDictionary<string, Guid> _connections = new();

    public void Connect(Guid sessionId, string connectionId) => _connections[connectionId] = sessionId;

    public void Disconnect(string connectionId) => _connections.TryRemove(connectionId, out _);

    public IReadOnlyCollection<Guid> ActiveSessions => _connections.Values.Distinct().ToList();
}
