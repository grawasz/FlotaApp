using Flota.Api.Auth;
using Flota.Api.Hubs;
using Flota.Api.Infrastructure.Data;
using Flota.Api.Infrastructure.Geocoding;
using Flota.Api.Infrastructure.UniAvl;
using Flota.Api.Infrastructure.UniAvl.Dto;
using Flota.Core.Contracts;
using Flota.Core.Geo;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Flota.Api.Services;

/// <summary>Stan floty: agregacja wywołań UniAVL w jeden dokument (<c>GET /fleet/snapshot</c>).</summary>
public sealed partial class FleetService(
    UniAvlClient avl,
    GeocodingService geocoding,
    IMemoryCache cache,
    IHubContext<FleetHub> hub,
    TimeProvider time,
    ILogger<FleetService> logger)
{
    /// <summary>Czas oczekiwania na adresy spoza pamięci podręcznej; pozostałe są dosyłane przez hub.</summary>
    static readonly TimeSpan AddressBudget = TimeSpan.FromSeconds(2);
    static readonly TimeSpan ObjectsLifetime = TimeSpan.FromMinutes(5);

    public async Task<FleetSnapshot> SnapshotAsync(ActiveSession session, CancellationToken ct)
    {
        var identityTask = avl.GetIdentityAsync(session.AvlToken, ct);
        var objectsTask = GetObjectsAsync(session, refresh: true, ct);
        var lastTask = avl.GetLastMessagesAsync(session.AvlToken, ct);
        var areasTask = avl.GetAreasAsync(session.AvlToken, ct);
        await Task.WhenAll(identityTask, objectsTask, lastTask, areasTask);

        var objects = objectsTask.Result;
        var matched = UniAvlMapper.MatchMessages(objects, lastTask.Result ?? []);
        var positions = objects.ToDictionary(o => o.TrackingObjectId,
            o => matched.TryGetValue(o.TrackingObjectId, out var m) ? UniAvlMapper.ToPosition(m, o.Tank?.Capacity) : null);

        // Adresy: pamięć podręczna i Targeo w limicie czasu; reszta w tle
        var points = positions.Values.Where(p => p is { ValidGps: true, HasLocation: true })
            .Select(p => new GeoPoint(p!.Latitude, p.Longitude)).ToList();
        var resolveTask = geocoding.ResolveAsync(points, CancellationToken.None);
        var completed = await Task.WhenAny(resolveTask, Task.Delay(AddressBudget, ct)) == resolveTask;
        var addresses = completed ? await resolveTask : new Dictionary<(int, int), AddressInfo>();
        if (!completed)
            _ = PushAddressesLaterAsync(session, resolveTask, positions);

        var fleet = objects.Select(o =>
        {
            var p = positions[o.TrackingObjectId];
            if (p is not null && addresses.TryGetValue(GeoConversions.AddressKey(p.Latitude, p.Longitude), out var a))
                p = p with { Address = a };
            return UniAvlMapper.ToFleetObject(o, p);
        }).ToList();

        var identity = identityTask.Result ?? new AvlIdentity { UserId = session.UserId, Login = session.Login };
        return new FleetSnapshot(
            time.GetUtcNow(),
            UniAvlMapper.ToUser(identity, session.Login),
            fleet,
            (areasTask.Result ?? []).Select(UniAvlMapper.ToArea).ToList());
    }

    /// <summary>Lista obiektów UniAVL użytkownika (pamięć podręczna 5 min — P4: jedno pobranie zamiast dwóch).</summary>
    public async Task<IReadOnlyList<AvlTrackingObject>> GetObjectsAsync(ActiveSession session, bool refresh, CancellationToken ct)
    {
        var key = $"objects:{session.Id}";
        if (!refresh && cache.TryGetValue(key, out IReadOnlyList<AvlTrackingObject>? cached) && cached is not null)
            return cached;
        var list = (IReadOnlyList<AvlTrackingObject>)(await avl.GetTrackingObjectsAsync(session.AvlToken, ct) ?? []);
        cache.Set(key, list, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ObjectsLifetime, Size = 1 });
        return list;
    }

    public async Task<FleetObject?> FindObjectAsync(ActiveSession session, int objectId, CancellationToken ct)
    {
        var o = (await GetObjectsAsync(session, refresh: false, ct)).FirstOrDefault(x => x.TrackingObjectId == objectId);
        return o is null ? null : UniAvlMapper.ToFleetObject(o, null);
    }

    async Task PushAddressesLaterAsync(ActiveSession session, Task<IReadOnlyDictionary<(int Lat, int Lon), AddressInfo>> resolve,
        IReadOnlyDictionary<int, ObjectPosition?> positions)
    {
        try
        {
            var addresses = await resolve;
            foreach (var (id, p) in positions)
            {
                if (p is null || !addresses.TryGetValue(GeoConversions.AddressKey(p.Latitude, p.Longitude), out var a)) continue;
                await hub.Clients.Group(FleetHub.Group(session.Id)).SendAsync(FleetHub.AddressResolvedMethod, new AddressResolved(id, p.Time, a));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogBackgroundAddresses(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nie udało się ustalić adresów w tle")]
    static partial void LogBackgroundAddresses(ILogger logger, Exception ex);
}

/// <summary>Ustawienia użytkownika (<c>GET/PUT /me/settings</c>).</summary>
public sealed class SettingsService(FlotaDbContext db)
{
    public async Task<UserSettings> GetAsync(int userId, CancellationToken ct)
    {
        var s = await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        return new UserSettings(s?.DefaultLatitude, s?.DefaultLongitude, s?.DefaultZoom);
    }

    public async Task SaveAsync(int userId, UserSettings settings, CancellationToken ct)
    {
        var s = await db.UserSettings.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (s is null)
        {
            s = new UserSettingsEntity { UserId = userId };
            db.UserSettings.Add(s);
        }
        s.DefaultLatitude = settings.DefaultLatitude;
        s.DefaultLongitude = settings.DefaultLongitude;
        s.DefaultZoom = settings.DefaultZoom;
        await db.SaveChangesAsync(ct);
    }
}
