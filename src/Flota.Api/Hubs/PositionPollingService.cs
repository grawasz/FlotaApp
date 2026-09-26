using System.Collections.Concurrent;
using Flota.Api.Auth;
using Flota.Api.Infrastructure.Geocoding;
using Flota.Api.Infrastructure.UniAvl;
using Flota.Api.Services;
using Flota.Core.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace Flota.Api.Hubs;

/// <summary>Konfiguracja strumienia pozycji.</summary>
public sealed class LiveOptions
{
    public const string Section = "Live";

    /// <summary>Okres odpytywania ostatnich pozycji dla sesji z aktywnym połączeniem huba.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Strumień pozycji: odpytywanie <c>api/TrackingMessages/Last</c> tokenem użytkownika i rozsyłanie wyłącznie zmienionych pozycji.
/// Rozwiązanie przejściowe do czasu rozstrzygnięcia K3 (uwierzytelnianie WebSocket UniAVL tokenem lub kontem technicznym).
/// </summary>
public sealed partial class PositionPollingService(
    LiveSessionRegistry registry,
    SessionStore sessions,
    IServiceScopeFactory scopes,
    IHubContext<FleetHub> hub,
    Microsoft.Extensions.Options.IOptions<LiveOptions> options,
    ILogger<PositionPollingService> logger) : BackgroundService
{
    /// <summary>Ostatnio wysłany czas wiadomości obiektu w sesji.</summary>
    readonly ConcurrentDictionary<(Guid Session, int Object), DateTime> _sent = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var active = registry.ActiveSessions;
            foreach (var key in _sent.Keys.Where(k => !active.Contains(k.Session)))
                _sent.TryRemove(key, out _);

            await Parallel.ForEachAsync(active, new ParallelOptions { CancellationToken = stoppingToken, MaxDegreeOfParallelism = 4 },
                async (sid, ct) =>
                {
                    try
                    {
                        await PollAsync(sid, ct);
                    }
                    catch (UniAvlUnauthorizedException)
                    {
                        await hub.Clients.Group(FleetHub.Group(sid)).SendAsync(FleetHub.ConnectionStateMethod, "unauthorized", ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        LogPollFailed(logger, ex);
                    }
                });
        }
    }

    async Task PollAsync(Guid sid, CancellationToken ct)
    {
        var session = await sessions.GetAsync(sid, ct);
        if (session is null) return;

        await using var scope = scopes.CreateAsyncScope();
        var avl = scope.ServiceProvider.GetRequiredService<UniAvlClient>();
        var fleet = scope.ServiceProvider.GetRequiredService<FleetService>();
        var geocoding = scope.ServiceProvider.GetRequiredService<GeocodingService>();

        var objects = await fleet.GetObjectsAsync(session, refresh: false, ct);
        var messages = await avl.GetLastMessagesAsync(session.AvlToken, ct) ?? [];
        var matched = UniAvlMapper.MatchMessages(objects, messages);

        var changed = new List<(int Id, ObjectPosition Position)>();
        foreach (var o in objects)
        {
            if (!matched.TryGetValue(o.TrackingObjectId, out var m)) continue;
            var key = (sid, o.TrackingObjectId);
            if (_sent.TryGetValue(key, out var last) && last >= m.Timedate) continue;
            _sent[key] = m.Timedate;
            changed.Add((o.TrackingObjectId, UniAvlMapper.ToPosition(m, o.Tank?.Capacity)));
        }
        if (changed.Count == 0) return;

        var withAddresses = await geocoding.WithAddressesAsync(changed.Select(c => c.Position).ToList(), ct);
        var payload = changed.Select((c, i) => new PositionChanged(c.Id, withAddresses[i])).ToList();
        await hub.Clients.Group(FleetHub.Group(sid)).SendAsync(FleetHub.PositionsChangedMethod, payload, ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Odpytywanie pozycji nie powiodło się")]
    static partial void LogPollFailed(ILogger logger, Exception ex);
}
