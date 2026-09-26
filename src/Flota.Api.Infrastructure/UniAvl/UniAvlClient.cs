using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Flota.Api.Infrastructure.UniAvl.Dto;
using Microsoft.Extensions.Logging;

namespace Flota.Api.Infrastructure.UniAvl;

/// <summary>Konfiguracja połączenia z UniAVL REST API.</summary>
public sealed class UniAvlOptions
{
    public const string Section = "UniAvl";

    /// <summary>Adres bazowy, np. <c>http://geo.uniavl.pl/AVL/</c>.</summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>Limit czasu pojedynczego żądania.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
}

/// <summary>Token UniAVL wygasł lub jest nieprawidłowy (HTTP 401).</summary>
public sealed class UniAvlUnauthorizedException : Exception
{
    public UniAvlUnauthorizedException() : base("Sesja UniAVL wygasła.") { }
    public UniAvlUnauthorizedException(string message) : base(message) { }
    public UniAvlUnauthorizedException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Błąd odpowiedzi UniAVL innej niż 401.</summary>
public sealed class UniAvlException : Exception
{
    public UniAvlException() { }
    public UniAvlException(string message) : base(message) { }
    public UniAvlException(string message, Exception inner) : base(message, inner) { }
    public HttpStatusCode? StatusCode { get; init; }
}

/// <summary>
/// Klient UniAVL REST API. Token użytkownika przekazywany w każdym wywołaniu (brak stanu współdzielonego — B2).
/// Odpowiedź 401 zgłaszana jako <see cref="UniAvlUnauthorizedException"/>; ponowne logowanie wykonuje użytkownik.
/// </summary>
public sealed partial class UniAvlClient(HttpClient http, ILogger<UniAvlClient> logger)
{
    const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
    const string DateFormat = "yyyy-MM-dd";
    static readonly UniAvlJsonContext Json = UniAvlJsonContext.Default;

    /// <summary>Logowanie: zwraca token UniAVL w postaci nagłówka <c>Token …</c> albo <c>null</c> przy błędnych danych.</summary>
    public async Task<string?> LoginAsync(string login, string password, CancellationToken ct)
    {
        var url = $"api/Login/Token?Login={Uri.EscapeDataString(login)}&Password={Uri.EscapeDataString(password)}";
        using var response = await http.GetAsync(new Uri(url, UriKind.Relative), ct).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
            throw new UniAvlException("Logowanie do UniAVL nie powiodło się.") { StatusCode = response.StatusCode };
        var body = (await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)).Trim().Trim('"');
        return string.IsNullOrEmpty(body) ? null : $"Token {body}";
    }

    public Task<AvlIdentity?> GetIdentityAsync(string token, CancellationToken ct) =>
        GetAsync("api/Login/Identity", token, Json.AvlIdentity, ct);

    public Task<List<AvlTrackingObject>?> GetTrackingObjectsAsync(string token, CancellationToken ct) =>
        GetAsync("api/TrackingObjects", token, Json.ListAvlTrackingObject, ct);

    public Task<List<AvlTrackingMessage>?> GetLastMessagesAsync(string token, CancellationToken ct) =>
        GetAsync("api/TrackingMessages/Last", token, Json.ListAvlTrackingMessage, ct);

    public Task<List<AvlArea>?> GetAreasAsync(string token, CancellationToken ct) =>
        GetAsync("api/Areas", token, Json.ListAvlArea, ct);

    public Task<List<AvlTrackingMessage>?> GetMessagesAsync(string token, int objectId, DateTime from, DateTime to, int? stageType, CancellationToken ct)
    {
        var q = new Query().Add("from", from).Add("to", to);
        if (stageType is not null) q.Add("stageType", stageType.Value);
        return GetAsync($"api/TrackingObjects/{objectId}/TrackingMessages{q}", token, Json.ListAvlTrackingMessage, ct);
    }

    public Task<AvlRoute?> GetRouteAsync(string token, int objectId, DateTime from, DateTime to,
        TimeSpan timeStop, TimeSpan timeIgnitionOff, decimal speed, bool stageSubtypeBySensor, CancellationToken ct)
    {
        var q = new Query().Add("from", from).Add("to", to).Add("timeStop", timeStop).Add("timeIgnitionOff", timeIgnitionOff)
            .Add("speed", speed).Add("StageSubtypeBySensor", stageSubtypeBySensor);
        return GetAsync($"api/TrackingObjects/{objectId}/TrackingMessages/Route{q}", token, Json.AvlRoute, ct);
    }

    public Task<AvlFuelReport?> GetFuelReportAsync(string token, int objectId, DateTime from, DateTime to, CancellationToken ct)
    {
        var q = new Query().Add("from", from).Add("to", to).Add("defueling", true);
        return GetAsync($"api/TrackingObjects/{objectId}/FuelReport{q}", token, Json.AvlFuelReport, ct);
    }

    public Task<List<AvlEvent>?> GetEventsAsync(string token, int objectId, DateTime from, DateTime to, CancellationToken ct)
    {
        var q = new Query().Add("From", from).Add("To", to);
        return GetAsync($"api/TrackingObjects/{objectId}/Events{q}", token, Json.ListAvlEvent, ct);
    }

    public Task<List<AvlEventTemplate>?> GetEventTemplatesAsync(string token, int objectId, CancellationToken ct) =>
        GetAsync($"api/TrackingObjects/{objectId}/EventTemplates", token, Json.ListAvlEventTemplate, ct);

    public Task<AvlDrivingStyle?> GetDrivingStyleAsync(string token, int objectId, DateTime from, DateTime to,
        TimeSpan timeStop, TimeSpan timeIgnitionOff, decimal speed,
        decimal speeding, decimal exceedRpm, decimal suddenBraking, decimal quickAcceleration, CancellationToken ct)
    {
        var q = new Query().Add("from", from).Add("to", to).Add("timeStop", timeStop).Add("timeIgnitionOff", timeIgnitionOff)
            .Add("speed", speed).Add("Speeding", speeding).Add("ExceedRpm", exceedRpm)
            .Add("SuddenBraking", suddenBraking).Add("QuickAcceleration", quickAcceleration);
        return GetAsync($"api/TrackingObjects/{objectId}/DrivingStyle{q}", token, Json.AvlDrivingStyle, ct);
    }

    public Task<AvlFleetReport?> GetFleetReportAsync(string token, int objectId, DateTime from, DateTime to, CancellationToken ct)
    {
        var q = $"?From={from.ToString(DateFormat, CultureInfo.InvariantCulture)}&To={to.ToString(DateFormat, CultureInfo.InvariantCulture)}";
        return SendAsync(HttpMethod.Post, $"api/TrackingObjects/{objectId}/FleetReport{q}", token, Json.AvlFleetReport, ct);
    }

    public Task<List<AvlLogbook>?> GetLogbooksAsync(string token, DateTime from, DateTime to, CancellationToken ct)
    {
        var q = $"?from={from.ToString(DateFormat, CultureInfo.InvariantCulture)}&to={to.ToString(DateFormat, CultureInfo.InvariantCulture)}";
        return GetAsync($"api/Logbooks{q}", token, Json.ListAvlLogbook, ct);
    }

    public Task<List<AvlLogbookDetails>?> GetLogbookDetailsAsync(string token, int logbookId, CancellationToken ct) =>
        GetAsync($"api/Logbooks/{logbookId}/Details", token, Json.ListAvlLogbookDetails, ct);

    public Task<List<AvlDriver>?> GetDriversAsync(string token, CancellationToken ct) =>
        GetAsync("api/Users/Drivers", token, Json.ListAvlDriver, ct);

    public Task<List<AvlEquipment>?> GetEquipmentAsync(string token, CancellationToken ct) =>
        GetAsync("api/Equipment", token, Json.ListAvlEquipment, ct);

    Task<T?> GetAsync<T>(string path, string token, JsonTypeInfo<T> type, CancellationToken ct) where T : class =>
        SendAsync(HttpMethod.Get, path, token, type, ct);

    async Task<T?> SendAsync<T>(HttpMethod method, string path, string token, JsonTypeInfo<T> type, CancellationToken ct) where T : class
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        // Nagłówek w postaci „Token <wartość>” — niestandardowy schemat UniAVL
        request.Headers.TryAddWithoutValidation("Authorization", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UniAvlUnauthorizedException();
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;
        if (!response.IsSuccessStatusCode)
        {
            LogFailure(logger, method.Method, StripQuery(path), (int)response.StatusCode);
            throw new UniAvlException("Usługa UniAVL zwróciła błąd.") { StatusCode = response.StatusCode };
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        if (response.Content.Headers.ContentLength == 0) return null;
        try
        {
            return await JsonSerializer.DeserializeAsync(stream, type, ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            LogInvalidJson(logger, StripQuery(path), ex);
            throw new UniAvlException("Nieprawidłowa odpowiedź usługi UniAVL.", ex);
        }
    }

    static string StripQuery(string path) => path.Split('?')[0];

    [LoggerMessage(Level = LogLevel.Warning, Message = "UniAVL {Method} {Path}: HTTP {Status}")]
    static partial void LogFailure(ILogger logger, string method, string path, int status);

    [LoggerMessage(Level = LogLevel.Error, Message = "UniAVL {Path}: nieprawidłowy JSON")]
    static partial void LogInvalidJson(ILogger logger, string path, Exception ex);

    /// <summary>Budowa parametrów zapytania niezależna od kultury.</summary>
    sealed class Query
    {
        readonly List<string> _parts = [];

        public Query Add(string name, DateTime value) => AddRaw(name, value.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
        public Query Add(string name, TimeSpan value) => AddRaw(name, value.ToString("c", CultureInfo.InvariantCulture));
        public Query Add(string name, decimal value) => AddRaw(name, value.ToString(CultureInfo.InvariantCulture));
        public Query Add(string name, int value) => AddRaw(name, value.ToString(CultureInfo.InvariantCulture));
        public Query Add(string name, bool value) => AddRaw(name, value ? "True" : "False");

        Query AddRaw(string name, string value)
        {
            _parts.Add($"{name}={Uri.EscapeDataString(value)}");
            return this;
        }

        public override string ToString() => _parts.Count == 0 ? string.Empty : "?" + string.Join('&', _parts);
    }
}
