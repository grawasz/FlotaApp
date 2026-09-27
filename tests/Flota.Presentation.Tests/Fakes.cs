using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using Flota.Client;
using Flota.Core.Contracts;
using Flota.Core.Geo;

namespace Flota.Presentation.Tests;

/// <summary>Flota.Api w pamięci: odpowiedzi według metody i ścieżki, zapis wywołań.</summary>
sealed class FakeApi : HttpMessageHandler
{
    readonly List<(HttpMethod Method, string Path, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _routes = [];

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Treści żądań (odczytane przed zwolnieniem żądania).</summary>
    public List<string> Bodies { get; } = [];

    public FakeApi On<T>(HttpMethod method, string path, T body, JsonTypeInfo<T> type) =>
        On(method, path, _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body, type) });

    public FakeApi On(HttpMethod method, string path, HttpStatusCode status) =>
        On(method, path, _ => new HttpResponseMessage(status));

    public FakeApi On(HttpMethod method, string path, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _routes.Insert(0, (method, path, respond));
        return this;
    }

    /// <summary>Liczba wywołań ścieżki (początek ścieżki bez parametrów).</summary>
    public int Count(string path) => Requests.Count(r => r.RequestUri!.AbsolutePath.StartsWith("/" + path, StringComparison.Ordinal));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        var route = _routes.FirstOrDefault(r => r.Method == request.Method && path.StartsWith(r.Path, StringComparison.Ordinal));
        return route.Respond is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : route.Respond(request);
    }

    public FlotaApiClient Client(TokenResponse? tokens = null)
    {
        var store = new InMemoryTokenStore();
        if (tokens is not null) store.SaveAsync(tokens).GetAwaiter().GetResult();
        return new FlotaApiClient(new HttpClient(this) { BaseAddress = new Uri("http://flota.test/") }, store);
    }
}

/// <summary>Strumień pozycji sterowany z testu.</summary>
sealed class FakeLiveFeed : IFleetLiveFeed
{
    public event EventHandler<IReadOnlyList<PositionChanged>>? PositionsChanged;
    public event EventHandler<AddressResolved>? AddressResolved;
    public event EventHandler<LiveConnectionState>? StateChanged;

    public LiveConnectionState State { get; private set; }
    public int Starts { get; private set; }
    public int Stops { get; private set; }

    public Task StartAsync(CancellationToken ct)
    {
        Starts++;
        Raise(LiveConnectionState.Connected);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        Stops++;
        Raise(LiveConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    public void Raise(LiveConnectionState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    public void Raise(params PositionChanged[] changes) => PositionsChanged?.Invoke(this, changes);

    public void Raise(AddressResolved address) => AddressResolved?.Invoke(this, address);
}

/// <summary>Magazyn stanu floty w pamięci.</summary>
sealed class MemorySnapshotStore : ISnapshotStore
{
    public FleetSnapshot? Stored { get; set; }
    public int Clears { get; private set; }

    public Task<FleetSnapshot?> LoadAsync() => Task.FromResult(Stored);

    public Task SaveAsync(FleetSnapshot snapshot)
    {
        Stored = snapshot;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Stored = null;
        Clears++;
        return Task.CompletedTask;
    }
}

/// <summary>Kontekst synchronizacji rejestrujący przekazane operacje (wątek interfejsu).</summary>
sealed class QueueContext : SynchronizationContext
{
    public Queue<(SendOrPostCallback Callback, object? State)> Posted { get; } = new();

    public override void Post(SendOrPostCallback d, object? state) => Posted.Enqueue((d, state));

    public void RunAll()
    {
        var previous = Current;
        SetSynchronizationContext(this);
        try
        {
            while (Posted.TryDequeue(out var p)) p.Callback(p.State);
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }
}

/// <summary>Dane testowe (fikcyjne).</summary>
static class Data
{
    public static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Unspecified);

    public static ObjectPosition Position(DateTime time, double lat = 52.23, double lon = 21.01, bool validGps = true, bool? engine = true, AddressInfo? address = null) => new()
    {
        Time = time,
        Latitude = lat,
        Longitude = lon,
        ValidGps = validGps,
        IsEngineStarted = engine,
        Speed = engine == true ? 40 : 0,
        Address = address,
    };

    public static FleetObject Object(int id, string name, ObjectPosition? position = null, string registration = "") => new()
    {
        Id = id,
        Name = name,
        RegistrationNumber = registration,
        IsActive = true,
        Position = position,
    };

    public static AddressInfo Address(string city) => new() { City = city, Street = "Prosta", House = "1" };

    public static FleetSnapshot Snapshot(params FleetObject[] objects) => new(
        new DateTimeOffset(Now),
        new UserInfo(900, "demo", "Jan", "Testowy", "Firma testowa"),
        objects,
        [
            new AreaInfo(1, "Zakład", true, "#FF0000", "#800000", 2, [new GeoPoint(52.0, 21.0), new GeoPoint(52.2, 21.2)]),
            new AreaInfo(2, "baza", true, "#00FF00", "#008000", 2, [new GeoPoint(50.0, 19.0)]),
            new AreaInfo(3, "Archiwalny", false, "#0000FF", "#000080", 2, [new GeoPoint(54.0, 18.0)]),
        ]);

    public static TokenResponse Tokens => new("access", "refresh", new DateTimeOffset(Now.AddHours(1)), new UserInfo(900, "demo", "Jan", "Testowy", null));
}
