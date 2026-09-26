using Microsoft.EntityFrameworkCore;

namespace Flota.Api.Infrastructure.Data;

/// <summary>Baza Flota.Api: pamięć podręczna adresów, ustawienia użytkowników, sesje.</summary>
public sealed class FlotaDbContext(DbContextOptions<FlotaDbContext> options) : DbContext(options)
{
    public DbSet<CachedAddress> Addresses => Set<CachedAddress>();
    public DbSet<UserSettingsEntity> UserSettings => Set<UserSettingsEntity>();
    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<CachedAddress>(e =>
        {
            e.ToTable("Addresses");
            e.HasKey(a => new { a.LatKey, a.LonKey });
            e.Property(a => a.Country).HasMaxLength(8);
            foreach (var p in new[] { nameof(CachedAddress.Province), nameof(CachedAddress.District), nameof(CachedAddress.Community),
                         nameof(CachedAddress.City), nameof(CachedAddress.Quarter), nameof(CachedAddress.Street) })
                e.Property(p).HasMaxLength(200);
            e.Property(a => a.PostalCode).HasMaxLength(16);
            e.Property(a => a.House).HasMaxLength(32);
        });

        modelBuilder.Entity<UserSettingsEntity>(e =>
        {
            e.ToTable("UserSettings");
            e.HasKey(s => s.UserId);
            e.Property(s => s.UserId).ValueGeneratedNever();
        });

        modelBuilder.Entity<SessionEntity>(e =>
        {
            e.ToTable("Sessions");
            e.HasKey(s => s.Id);
            e.Property(s => s.Login).HasMaxLength(100);
            e.Property(s => s.RefreshTokenHash).HasMaxLength(64);
            e.HasIndex(s => s.RefreshTokenHash).IsUnique();
            e.HasIndex(s => s.UserId);
        });
    }
}

/// <summary>Adres z geokodowania; klucz — współrzędne × 10 000 (4 miejsca po przecinku).</summary>
public sealed class CachedAddress
{
    public int LatKey { get; set; }
    public int LonKey { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Community { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Quarter { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string House { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Ustawienia użytkownika: widok początkowy mapy.</summary>
public sealed class UserSettingsEntity
{
    public int UserId { get; set; }
    public double? DefaultLatitude { get; set; }
    public double? DefaultLongitude { get; set; }
    public double? DefaultZoom { get; set; }
}

/// <summary>Sesja użytkownika: zaszyfrowany token UniAVL i skrót tokenu odświeżającego.</summary>
public sealed class SessionEntity
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public string Login { get; set; } = string.Empty;
    /// <summary>Token UniAVL chroniony przez Data Protection.</summary>
    public string ProtectedAvlToken { get; set; } = string.Empty;
    /// <summary>SHA-256 tokenu odświeżającego (hex).</summary>
    public string RefreshTokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
