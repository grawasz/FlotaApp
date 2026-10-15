using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using Flota.Core.Contracts;

namespace Flota.Client;

/// <summary>Błąd wywołania Flota.Api z komunikatem dla użytkownika.</summary>
public sealed class FlotaApiException : Exception
{
    public FlotaApiException() { }
    public FlotaApiException(string message) : base(message) { }
    public FlotaApiException(string message, Exception inner) : base(message, inner) { }
    public HttpStatusCode? StatusCode { get; init; }
}

/// <summary>Sesja wygasła — wymagane ponowne logowanie.</summary>
public sealed class SessionExpiredException : Exception
{
    public SessionExpiredException() : base("Sesja wygasła. Zaloguj się ponownie.") { }
    public SessionExpiredException(string message) : base(message) { }
    public SessionExpiredException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Klient Flota.Api. Token dostępowy odnawiany automatycznie po odpowiedzi 401.
/// Tryb ciasteczkowy (<paramref name="cookieSession"/>, wersja przeglądarkowa): token odświeżający w ciasteczku <c>HttpOnly</c>
/// ustawianym przez Flota.Api — klient nie przechowuje go i odnawia sesję również bez zapisanych tokenów (po przeładowaniu strony).
/// </summary>
public sealed class FlotaApiClient(HttpClient http, ITokenStore tokens, bool cookieSession = false) : IDisposable
{
    static readonly FlotaJsonContext Json = FlotaJsonContext.Default;
    // Opcje fetch przeglądarki (BrowserHttpHandler): ciasteczka także dla serwera deweloperskiego spod innego adresu
    static readonly HttpRequestOptionsKey<IDictionary<string, object>> FetchOptions = new("WebAssemblyFetchOptions");
    readonly SemaphoreSlim _refreshLock = new(1, 1);

    /// <summary>Tryb ciasteczkowy (token odświeżający w ciasteczku <c>HttpOnly</c>).</summary>
    public bool CookieSession => cookieSession;

    public event EventHandler? SessionExpired;

    public Uri? BaseAddress => http.BaseAddress;

    /// <summary>Aktualny token dostępowy (połączenie huba).</summary>
    public string? AccessToken => tokens.Current?.AccessToken;

    public async Task<TokenResponse?> LoginAsync(string login, string password, CancellationToken ct)
    {
        using var request = AuthRequest("auth/login", JsonContent.Create(new LoginRequest(login, password), Json.LoginRequest));
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return null;
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
        var result = await response.Content.ReadFromJsonAsync(Json.TokenResponse, ct).ConfigureAwait(false)
            ?? throw new FlotaApiException("Nieprawidłowa odpowiedź serwera.");
        await tokens.SaveAsync(result).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Odnowienie tokenu; <c>false</c>, gdy sesja wygasła. W trybie ciasteczkowym również bez zapisanych tokenów
    /// (wznowienie sesji po przeładowaniu strony).
    /// </summary>
    public async Task<bool> RefreshAsync(CancellationToken ct)
    {
        var current = tokens.Current;
        if (current is null && !cookieSession) return false;
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(current, tokens.Current)) return tokens.Current is not null;
            var presented = cookieSession ? string.Empty : current!.RefreshToken;
            using var request = AuthRequest("auth/refresh", JsonContent.Create(new RefreshRequest(presented), Json.RefreshRequest));
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            {
                await tokens.ClearAsync().ConfigureAwait(false);
                return false;
            }
            await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
            var next = await response.Content.ReadFromJsonAsync(Json.TokenResponse, ct).ConfigureAwait(false);
            if (next is null) return false;
            await tokens.SaveAsync(next).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task LogoutAsync(CancellationToken ct)
    {
        try
        {
            using var _ = await SendAsync(() => AuthRequest("auth/logout", null), ct, throwOnExpired: false).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            // Wylogowanie lokalne również bez połączenia
        }
        finally
        {
            await tokens.ClearAsync().ConfigureAwait(false);
        }
    }

    public Task<FleetSnapshot> GetSnapshotAsync(CancellationToken ct) => GetAsync("fleet/snapshot", Json.FleetSnapshot, ct);

    public Task<UserSettings> GetSettingsAsync(CancellationToken ct) => GetAsync("me/settings", Json.UserSettings, ct);

    public async Task SaveSettingsAsync(UserSettings settings, CancellationToken ct)
    {
        using var _ = await SendAsync(() => new HttpRequestMessage(HttpMethod.Put, "me/settings") { Content = JsonContent.Create(settings, Json.UserSettings) }, ct).ConfigureAwait(false);
    }

    public Task<RouteReport?> GetRouteAsync(int objectId, RouteQuery q, CancellationToken ct) =>
        GetOptionalAsync($"objects/{objectId}/route{RouteQueryString(q)}", Json.RouteReport, ct);

    public Task<WaypointsResponse> GetWaypointsAsync(int objectId, IReadOnlyList<StageWindow> stages, CancellationToken ct) =>
        PostAsync($"objects/{objectId}/waypoints", new WaypointsRequest(stages), Json.WaypointsRequest, Json.WaypointsResponse, ct);

    public Task<IReadOnlyList<ObjectPosition>> GetTraceAsync(int objectId, DateTime from, DateTime to, CancellationToken ct) =>
        GetAsync($"objects/{objectId}/trace?from={D(from)}&to={D(to)}", Json.IReadOnlyListObjectPosition, ct);

    public Task<FuelReport?> GetFuelAsync(int objectId, DateTime from, DateTime to, CancellationToken ct) =>
        GetOptionalAsync($"objects/{objectId}/fuel?from={D(from)}&to={D(to)}", Json.FuelReport, ct);

    public Task<IReadOnlyList<ObjectEvent>> GetEventsAsync(int objectId, DateTime from, DateTime to, CancellationToken ct) =>
        GetAsync($"objects/{objectId}/events?from={D(from)}&to={D(to)}", Json.IReadOnlyListObjectEvent, ct);

    public Task<DrivingStyle?> GetDrivingStyleAsync(int objectId, DrivingStyleQuery q, CancellationToken ct) =>
        GetOptionalAsync(string.Create(CultureInfo.InvariantCulture,
            $"objects/{objectId}/driving-style{RouteQueryString(q.Route)}&speeding={q.Speeding}&exceedRpm={q.ExceedRpm}&suddenBraking={q.SuddenBraking}&quickAcceleration={q.QuickAcceleration}"),
            Json.DrivingStyle, ct);

    public Task<IReadOnlyList<RouteStage>> GetStageSummaryAsync(int objectId, RouteQuery q, CancellationToken ct) =>
        GetAsync($"objects/{objectId}/stage-summary{RouteQueryString(q)}", Json.IReadOnlyListRouteStage, ct);

    public Task<FleetReportResponse> GetFleetReportAsync(FleetReportRequest request, CancellationToken ct) =>
        PostAsync("reports/fleet", request, Json.FleetReportRequest, Json.FleetReportResponse, ct);

    public Task<IReadOnlyList<EcoDrivingRow>> GetEcoDrivingAsync(EcoDrivingRequest request, CancellationToken ct) =>
        PostAsync("reports/eco-driving", request, Json.EcoDrivingRequest, Json.IReadOnlyListEcoDrivingRow, ct);

    public Task<IReadOnlyList<LogbookInfo>> GetLogbooksAsync(DateTime from, DateTime to, CancellationToken ct) =>
        GetAsync($"logbooks?from={D(from)}&to={D(to)}", Json.IReadOnlyListLogbookInfo, ct);

    public Task<IReadOnlyList<LogbookEntry>> GetLogbookEntriesAsync(int logbookId, CancellationToken ct) =>
        GetAsync($"logbooks/{logbookId}", Json.IReadOnlyListLogbookEntry, ct);

    public Task<IReadOnlyList<DriverInfo>> GetDriversAsync(CancellationToken ct) =>
        GetAsync("dictionaries/drivers", Json.IReadOnlyListDriverInfo, ct);

    public Task<IReadOnlyList<EquipmentInfo>> GetEquipmentAsync(bool refresh, CancellationToken ct) =>
        GetAsync($"dictionaries/equipment?refresh={(refresh ? "true" : "false")}", Json.IReadOnlyListEquipmentInfo, ct);

    public Task<IReadOnlyList<EventTemplateInfo>> GetEventTemplatesAsync(IReadOnlyList<int> objectIds, CancellationToken ct) =>
        PostAsync("dictionaries/event-templates", new EventTemplatesRequest(objectIds), Json.EventTemplatesRequest, Json.IReadOnlyListEventTemplateInfo, ct);

    /// <summary>Plik XLSX z tabeli.</summary>
    public async Task<byte[]> ExportXlsxAsync(ExportTable table, CancellationToken ct)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, "export/xlsx") { Content = JsonContent.Create(table, Json.ExportTable) }, ct).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    public void Dispose() => _refreshLock.Dispose();

    static string D(DateTime value) => Uri.EscapeDataString(value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));

    static string RouteQueryString(RouteQuery q)
    {
        var s = $"?from={D(q.From)}&to={D(q.To)}&stageSubtypeBySensor={(q.StageSubtypeBySensor ? "true" : "false")}";
        if (q.StopTime is { } st) s += $"&stopTime={Uri.EscapeDataString(st.ToString("c", CultureInfo.InvariantCulture))}&minSpeed={q.MinSpeed.ToString(CultureInfo.InvariantCulture)}";
        if (q.IgnitionOffTime is { } it) s += $"&ignitionOffTime={Uri.EscapeDataString(it.ToString("c", CultureInfo.InvariantCulture))}";
        return s;
    }

    async Task<T> GetAsync<T>(string path, JsonTypeInfo<T> type, CancellationToken ct)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, path), ct).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync(type, ct).ConfigureAwait(false) ?? throw new FlotaApiException("Nieprawidłowa odpowiedź serwera.");
    }

    async Task<T?> GetOptionalAsync<T>(string path, JsonTypeInfo<T> type, CancellationToken ct) where T : class
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, path), ct, allowNotFound: true).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        return await response.Content.ReadFromJsonAsync(type, ct).ConfigureAwait(false);
    }

    async Task<TResult> PostAsync<TBody, TResult>(string path, TBody body, JsonTypeInfo<TBody> bodyType, JsonTypeInfo<TResult> resultType, CancellationToken ct)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, bodyType) }, ct).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync(resultType, ct).ConfigureAwait(false) ?? throw new FlotaApiException("Nieprawidłowa odpowiedź serwera.");
    }

    async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> create, CancellationToken ct, bool allowNotFound = false, bool throwOnExpired = true)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = create();
            if (tokens.Current?.AccessToken is { } token)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                if (attempt == 0 && await RefreshAsync(ct).ConfigureAwait(false)) continue;
                if (!throwOnExpired) return new HttpResponseMessage(HttpStatusCode.NoContent);
                SessionExpired?.Invoke(this, EventArgs.Empty);
                throw new SessionExpiredException();
            }
            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound) return response;
            await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
            return response;
        }
    }

    /// <summary>Żądanie <c>/auth/*</c>; w trybie ciasteczkowym nagłówek trybu i ciasteczka w żądaniu przeglądarki.</summary>
    HttpRequestMessage AuthRequest(string path, HttpContent? content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        if (cookieSession)
        {
            request.Headers.Add(AuthSession.SessionModeHeader, AuthSession.CookieMode);
            request.Options.Set(FetchOptions, new Dictionary<string, object> { ["credentials"] = "include" });
        }
        return request;
    }

    static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string message;
        try
        {
            message = (await response.Content.ReadFromJsonAsync(Json.ErrorResponse, ct).ConfigureAwait(false))?.Message ?? DefaultMessage(response.StatusCode);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException or InvalidOperationException)
        {
            message = DefaultMessage(response.StatusCode);
        }
        var status = response.StatusCode;
        response.Dispose();
        throw new FlotaApiException(message) { StatusCode = status };
    }

    static string DefaultMessage(HttpStatusCode status) => (int)status >= 500
        ? "Serwer jest chwilowo niedostępny. Spróbuj ponownie później."
        : "Nie udało się wykonać operacji.";
}
