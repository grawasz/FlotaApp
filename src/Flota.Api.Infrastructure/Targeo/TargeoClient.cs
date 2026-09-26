using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using Flota.Api.Infrastructure.Targeo.Soap;
using Flota.Core.Contracts;
using Flota.Core.Geo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flota.Api.Infrastructure.Targeo;

/// <summary>Konfiguracja Targeo — wartości wyłącznie w User Secrets lub magazynie sekretów (B1).</summary>
public sealed class TargeoOptions
{
    public const string Section = "Targeo";

    /// <summary>Adres usługi SOAP (zawiera klucz dostępu).</summary>
    public string? Endpoint { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? Salt { get; set; }
    /// <summary>Liczba punktów w jednym wywołaniu <c>revGeocodeMulti</c>.</summary>
    public int BatchSize { get; set; } = 50;
    /// <summary>Liczba równoczesnych wywołań usługi.</summary>
    public int MaxConcurrency { get; set; } = 2;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(User) &&
        !string.IsNullOrWhiteSpace(Password) && Salt is not null;
}

/// <summary>Geokodowanie odwrotne punktów.</summary>
public interface IReverseGeocoder
{
    bool IsAvailable { get; }

    /// <summary>Adresy punktów w kolejności wejściowej; <c>null</c> dla punktów bez adresu.</summary>
    Task<IReadOnlyList<AddressInfo?>> ReverseAsync(IReadOnlyList<GeoPoint> points, CancellationToken ct);
}

/// <summary>
/// Klient Targeo: jedna długotrwała sesja (<c>secureLogin</c>), ponowne logowanie wyłącznie po błędzie,
/// zapytania wsadowe <c>revGeocodeMulti</c> z ograniczoną współbieżnością (P1, P2).
/// </summary>
public sealed partial class TargeoClient : IReverseGeocoder, IDisposable
{
    static readonly paramsRevGeo Params = new() { snapToBld = false, getPostalCode = false };

    readonly TargeoOptions _options;
    readonly ILogger<TargeoClient> _logger;
    readonly SemaphoreSlim _concurrency;
    readonly SemaphoreSlim _sessionLock = new(1, 1);
    TargeoSOAPClient? _client;

    public TargeoClient(IOptions<TargeoOptions> options, ILogger<TargeoClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger;
        _concurrency = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrency));
    }

    public bool IsAvailable => _options.IsConfigured;

    public async Task<IReadOnlyList<AddressInfo?>> ReverseAsync(IReadOnlyList<GeoPoint> points, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(points);
        var result = new AddressInfo?[points.Count];
        if (!IsAvailable || points.Count == 0) return result;

        var batchSize = Math.Max(1, _options.BatchSize);
        var batches = Enumerable.Range(0, (points.Count + batchSize - 1) / batchSize)
            .Select(b => (Start: b * batchSize, Count: Math.Min(batchSize, points.Count - b * batchSize)));

        await Parallel.ForEachAsync(batches, new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Math.Max(1, _options.MaxConcurrency) },
            async (batch, token) =>
            {
                var slice = new List<GeoPoint>(batch.Count);
                for (var i = 0; i < batch.Count; i++) slice.Add(points[batch.Start + i]);
                var addresses = await ReverseBatchAsync(slice, token).ConfigureAwait(false);
                for (var i = 0; i < addresses.Count; i++) result[batch.Start + i] = addresses[i];
            }).ConfigureAwait(false);
        return result;
    }

    async Task<IReadOnlyList<AddressInfo?>> ReverseBatchAsync(IReadOnlyList<GeoPoint> points, CancellationToken ct)
    {
        await _concurrency.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var client = await GetSessionAsync(attempt > 0, ct).ConfigureAwait(false);
                try
                {
                    var gps = points.Select(p => new pointGPS { x = p.Longitude, y = p.Latitude }).ToArray();
                    if (gps.Length == 1)
                    {
                        var single = await client.revGeocodeAsync(gps[0], Params).ConfigureAwait(false);
                        return [Map(single.country)];
                    }
                    var multi = await client.revGeocodeMultiAsync(gps, Params).ConfigureAwait(false);
                    var list = multi.province ?? [];
                    return points.Select((_, i) => i < list.Length ? Map(list[i]) : null).ToList();
                }
                catch (Exception ex) when (ex is CommunicationException or TimeoutException)
                {
                    LogCallFailed(_logger, attempt + 1, ex);
                    ResetSession();
                }
            }
            return new AddressInfo?[points.Count];
        }
        finally
        {
            _concurrency.Release();
        }
    }

    async Task<TargeoSOAPClient> GetSessionAsync(bool renew, CancellationToken ct)
    {
        await _sessionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (renew) ResetSession();
            if (_client is not null) return _client;

            var client = new TargeoSOAPClient(TargeoSOAPClient.EndpointConfiguration.TargeoSOAPService, _options.Endpoint!);
            await client.OpenAsync().ConfigureAwait(false);
            var token = SessionToken();
            await client.secureLoginAsync(_options.User!, SecurePassword(_options.Salt!, _options.Password!, token), token).ConfigureAwait(false);
            _client = client;
            return client;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    void ResetSession()
    {
        var old = Interlocked.Exchange(ref _client, null);
        if (old is null) return;
        try { old.Abort(); } catch (CommunicationException) { }
    }

    static AddressInfo? Map(address? a) => a is null ? null : new AddressInfo
    {
        Country = a.country ?? string.Empty,
        Province = a.province ?? string.Empty,
        District = a.district ?? string.Empty,
        Community = a.community ?? string.Empty,
        City = a.city ?? string.Empty,
        Quarter = a.quarter ?? string.Empty,
        PostalCode = a.pcode ?? string.Empty,
        Street = a.street ?? string.Empty,
        House = a.house ?? string.Empty,
    };

    /// <summary>Token sesji: 8 pierwszych znaków MD5 losowego identyfikatora (protokół Targeo).</summary>
    static string SessionToken() => Md5Hex(Guid.NewGuid().ToString())[..8];

    /// <summary>Hasło zabezpieczone: MD5(sól + MD5(hasło) + token) — wymóg protokołu Targeo.</summary>
    internal static string SecurePassword(string salt, string password, string token) =>
        Md5Hex(salt + Md5Hex(password) + token);

#pragma warning disable CA5351 // MD5 wymagany przez protokół uwierzytelniania Targeo
    static string Md5Hex(string value) =>
        Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(value)));
#pragma warning restore CA5351

    public void Dispose()
    {
        ResetSession();
        _concurrency.Dispose();
        _sessionLock.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Targeo: błąd wywołania (próba {Attempt}); sesja zostanie odnowiona")]
    static partial void LogCallFailed(ILogger logger, int attempt, Exception ex);
}
