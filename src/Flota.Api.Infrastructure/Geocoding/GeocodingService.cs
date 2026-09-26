using Flota.Api.Infrastructure.Data;
using Flota.Api.Infrastructure.Targeo;
using Flota.Core.Contracts;
using Flota.Core.Geo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flota.Api.Infrastructure.Geocoding;

/// <summary>
/// Geokodowanie odwrotne z trójpoziomową pamięcią podręczną: pamięć operacyjna → baza → Targeo (plan, 5.2).
/// Klucz: współrzędne zaokrąglone do 4 miejsc po przecinku.
/// </summary>
public sealed partial class GeocodingService(
    IReverseGeocoder geocoder,
    IMemoryCache memory,
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<GeocodingService> logger)
{
    static readonly MemoryCacheEntryOptions MemoryEntry = new()
    {
        SlidingExpiration = TimeSpan.FromHours(6),
        Size = 1,
    };

    /// <summary>Adresy punktów; punkty bez adresu nie występują w wyniku.</summary>
    public async Task<IReadOnlyDictionary<(int Lat, int Lon), AddressInfo>> ResolveAsync(IEnumerable<GeoPoint> points, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(points);
        var result = new Dictionary<(int, int), AddressInfo>();
        var missing = new Dictionary<(int, int), GeoPoint>();

        foreach (var p in points)
        {
            if (p.Latitude == 0 && p.Longitude == 0) continue;
            var key = GeoConversions.AddressKey(p.Latitude, p.Longitude);
            if (result.ContainsKey(key) || missing.ContainsKey(key)) continue;
            if (memory.TryGetValue(CacheKey(key), out AddressInfo? cached) && cached is not null)
                result[key] = cached;
            else
                missing[key] = p;
        }
        if (missing.Count == 0) return result;

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlotaDbContext>();

        // Poziom 2: baza
        var lats = missing.Keys.Select(k => k.Item1).Distinct().ToList();
        var stored = await db.Addresses.AsNoTracking()
            .Where(a => lats.Contains(a.LatKey))
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var a in stored)
        {
            var key = (a.LatKey, a.LonKey);
            if (!missing.Remove(key)) continue;
            var info = ToInfo(a);
            result[key] = info;
            memory.Set(CacheKey(key), info, MemoryEntry);
        }
        if (missing.Count == 0 || !geocoder.IsAvailable) return result;

        // Poziom 3: Targeo (zapytania wsadowe)
        var keys = missing.Keys.ToList();
        var resolved = await geocoder.ReverseAsync(keys.Select(k => missing[k]).ToList(), ct).ConfigureAwait(false);
        var now = time.GetUtcNow();
        var added = 0;
        for (var i = 0; i < keys.Count; i++)
        {
            var info = resolved[i];
            if (info is null || info.IsEmpty) continue;
            result[keys[i]] = info;
            memory.Set(CacheKey(keys[i]), info, MemoryEntry);
            db.Addresses.Add(FromInfo(keys[i], info, now));
            added++;
        }
        if (added > 0)
        {
            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (DbUpdateException ex)
            {
                // Równoległe zapytania mogły już zapisać te same adresy
                LogSaveConflict(logger, ex);
            }
        }
        LogResolved(logger, keys.Count, added);
        return result;
    }

    /// <summary>Uzupełnienie adresów pozycji.</summary>
    public async Task<IReadOnlyList<ObjectPosition>> WithAddressesAsync(IReadOnlyList<ObjectPosition> positions, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(positions);
        var map = await ResolveAsync(positions.Where(p => p.ValidGps && p.HasLocation).Select(p => new GeoPoint(p.Latitude, p.Longitude)), ct).ConfigureAwait(false);
        return positions.Select(p => p.Address is null && map.TryGetValue(GeoConversions.AddressKey(p.Latitude, p.Longitude), out var a) ? p with { Address = a } : p).ToList();
    }

    /// <summary>Adres jednego punktu wyłącznie z pamięci podręcznej (bez zapytań).</summary>
    public AddressInfo? TryGetCached(double latitude, double longitude) =>
        memory.TryGetValue(CacheKey(GeoConversions.AddressKey(latitude, longitude)), out AddressInfo? a) ? a : null;

    static string CacheKey((int Lat, int Lon) key) => $"addr:{key.Lat}:{key.Lon}";

    static AddressInfo ToInfo(CachedAddress a) => new()
    {
        Country = a.Country, Province = a.Province, District = a.District, Community = a.Community, City = a.City,
        Quarter = a.Quarter, PostalCode = a.PostalCode, Street = a.Street, House = a.House,
    };

    static CachedAddress FromInfo((int Lat, int Lon) key, AddressInfo a, DateTimeOffset now) => new()
    {
        LatKey = key.Lat, LonKey = key.Lon,
        Country = a.Country, Province = a.Province, District = a.District, Community = a.Community, City = a.City,
        Quarter = a.Quarter, PostalCode = a.PostalCode, Street = a.Street, House = a.House, CreatedAt = now,
    };

    [LoggerMessage(Level = LogLevel.Debug, Message = "Geokodowanie: {Requested} punktów spoza pamięci podręcznej, zapisano {Added}")]
    static partial void LogResolved(ILogger logger, int requested, int added);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Geokodowanie: konflikt zapisu pamięci podręcznej")]
    static partial void LogSaveConflict(ILogger logger, Exception ex);
}
