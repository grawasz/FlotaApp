using Flota.Api.Infrastructure.Data;
using Flota.Api.Infrastructure.Geocoding;
using Flota.Api.Infrastructure.Targeo;
using Flota.Core.Contracts;
using Flota.Core.Geo;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Flota.Api.Tests;

public sealed class GeocodingServiceTests : IDisposable
{
    readonly SqliteConnection _connection = new("Data Source=:memory:");
    readonly ServiceProvider _services;
    readonly IReverseGeocoder _geocoder = Substitute.For<IReverseGeocoder>();
    readonly MemoryCache _memory = new(new MemoryCacheOptions { SizeLimit = 1000 });

    public GeocodingServiceTests()
    {
        _connection.Open();
        _services = new ServiceCollection()
            .AddDbContext<FlotaDbContext>(o => o.UseSqlite(_connection))
            .BuildServiceProvider();
        using (var scope = _services.CreateScope())
            scope.ServiceProvider.GetRequiredService<FlotaDbContext>().Database.EnsureCreated();
        _geocoder.IsAvailable.Returns(true);
        _geocoder.ReverseAsync(Arg.Any<IReadOnlyList<GeoPoint>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<AddressInfo?>>(
                ci.Arg<IReadOnlyList<GeoPoint>>().Select(p => (AddressInfo?)new AddressInfo { City = "Miasto", Street = $"Ulica {p.Latitude}" }).ToList()));
    }

    public void Dispose()
    {
        _services.Dispose();
        _memory.Dispose();
        _connection.Dispose();
    }

    GeocodingService Create(IMemoryCache? memory = null) => new(_geocoder, memory ?? _memory,
        _services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, NullLogger<GeocodingService>.Instance);

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Resolve_DeduplicatesByKey_AndSkipsZeroPoints()
    {
        var map = await Create().ResolveAsync([new(52.23001, 21.01), new(52.23002, 21.01), new(0, 0)], Ct);
        Assert.Single(map);
        await _geocoder.Received(1).ReverseAsync(Arg.Is<IReadOnlyList<GeoPoint>>(l => l.Count == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_SecondCall_UsesMemoryCache()
    {
        var service = Create();
        await service.ResolveAsync([new(52.23, 21.01)], Ct);
        await service.ResolveAsync([new(52.23, 21.01)], Ct);
        await _geocoder.Received(1).ReverseAsync(Arg.Any<IReadOnlyList<GeoPoint>>(), Arg.Any<CancellationToken>());
        Assert.NotNull(service.TryGetCached(52.23, 21.01));
    }

    [Fact]
    public async Task Resolve_AfterRestart_UsesDatabase()
    {
        await Create().ResolveAsync([new(52.23, 21.01)], Ct);
        using var freshMemory = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
        var map = await Create(freshMemory).ResolveAsync([new(52.23, 21.01)], Ct);
        Assert.Equal("Miasto", Assert.Single(map).Value.City);
        await _geocoder.Received(1).ReverseAsync(Arg.Any<IReadOnlyList<GeoPoint>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_GeocoderUnavailable_ReturnsEmpty()
    {
        _geocoder.IsAvailable.Returns(false);
        var map = await Create().ResolveAsync([new(52.23, 21.01)], Ct);
        Assert.Empty(map);
        await _geocoder.DidNotReceive().ReverseAsync(Arg.Any<IReadOnlyList<GeoPoint>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_EmptyAddress_NotCached()
    {
        _geocoder.ReverseAsync(Arg.Any<IReadOnlyList<GeoPoint>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AddressInfo?>>([null]));
        var service = Create();
        Assert.Empty(await service.ResolveAsync([new(52.23, 21.01)], Ct));
        Assert.Null(service.TryGetCached(52.23, 21.01));
    }
}
