using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Flota.Api.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Flota.Api.Auth;

/// <summary>Konfiguracja tokenów Flota.Api.</summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";

    public string Issuer { get; set; } = "flota-api";
    public string Audience { get; set; } = "flota-app";
    /// <summary>Klucz podpisu JWT (min. 32 bajty, Base64). Brak — klucz losowy na czas działania procesu.</summary>
    public string? SigningKey { get; set; }
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(30);
}

/// <summary>Sesja użytkownika w pamięci (odszyfrowany token UniAVL).</summary>
public sealed record ActiveSession(Guid Id, int UserId, string Login, string AvlToken);

/// <summary>
/// Rejestr sesji: token UniAVL zaszyfrowany (Data Protection) w bazie, token odświeżający przechowywany jako skrót SHA-256.
/// </summary>
public sealed class SessionStore(IServiceScopeFactory scopes, IDataProtectionProvider protection, TimeProvider time, Microsoft.Extensions.Options.IOptions<AuthOptions> options)
{
    readonly IDataProtector _protector = protection.CreateProtector("Flota.Api.AvlToken");
    readonly ConcurrentDictionary<Guid, ActiveSession> _cache = new();
    readonly AuthOptions _options = options.Value;

    /// <summary>Utworzenie sesji; zwraca sesję i token odświeżający (jawny, tylko dla klienta).</summary>
    public async Task<(ActiveSession Session, string RefreshToken)> CreateAsync(int userId, string login, string avlToken, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var refresh = NewRefreshToken();
        var entity = new SessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Login = login,
            ProtectedAvlToken = _protector.Protect(avlToken),
            RefreshTokenHash = Hash(refresh),
            CreatedAt = now,
            LastUsedAt = now,
            ExpiresAt = now + _options.SessionLifetime,
        };
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlotaDbContext>();
        db.Sessions.Add(entity);
        await db.SaveChangesAsync(ct);
        var session = new ActiveSession(entity.Id, userId, login, avlToken);
        _cache[entity.Id] = session;
        return (session, refresh);
    }

    /// <summary>Sesja aktywna o podanym identyfikatorze albo <c>null</c>.</summary>
    public async Task<ActiveSession?> GetAsync(Guid sessionId, CancellationToken ct)
    {
        if (_cache.TryGetValue(sessionId, out var cached)) return cached;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlotaDbContext>();
        var e = await db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (e is null || e.RevokedAt is not null || e.ExpiresAt < time.GetUtcNow()) return null;
        return _cache[sessionId] = ToSession(e);
    }

    /// <summary>Rotacja tokenu odświeżającego; <c>null</c>, gdy token nieważny.</summary>
    public async Task<(ActiveSession Session, string RefreshToken)?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlotaDbContext>();
        var e = await db.Sessions.FirstOrDefaultAsync(s => s.RefreshTokenHash == hash, ct);
        var now = time.GetUtcNow();
        if (e is null || e.RevokedAt is not null || e.ExpiresAt < now) return null;

        var next = NewRefreshToken();
        e.RefreshTokenHash = Hash(next);
        e.LastUsedAt = now;
        e.ExpiresAt = now + _options.SessionLifetime;
        await db.SaveChangesAsync(ct);
        var session = _cache[e.Id] = ToSession(e);
        return (session, next);
    }

    public async Task RevokeAsync(Guid sessionId, CancellationToken ct)
    {
        _cache.TryRemove(sessionId, out _);
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlotaDbContext>();
        await db.Sessions.Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.RevokedAt, time.GetUtcNow()), ct);
    }

    ActiveSession ToSession(SessionEntity e) => new(e.Id, e.UserId, e.Login, _protector.Unprotect(e.ProtectedAvlToken));

    static string NewRefreshToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
