using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Flota.Core.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Flota.Api.Tests;

/// <summary>
/// Sesja w przeglądarce (T9.2): token odświeżający w ciasteczku HttpOnly, wznowienie po przeładowaniu strony, wylogowanie,
/// odrzucenie żądania bez ciasteczka; nagłówki odwrotnego proxy z zaufanej i niezaufanej sieci.
/// </summary>
public sealed class CookieSessionTests : IDisposable
{
    const string RemoteIpHeader = "X-Test-Remote-IP";
    readonly WireMockServer _avl = WireMockServer.Start();
    readonly string _dir = Path.Combine(Path.GetTempPath(), "flota-cookie-tests-" + Guid.NewGuid().ToString("N"));
    readonly WebApplicationFactory<Program> _factory;

    public CookieSessionTests()
    {
        _avl.Given(Request.Create().WithPath("/AVL/api/Login/Token").WithParam("Login", "demo").UsingGet())
            .RespondWith(Response.Create().WithBody("\"avl-token\""));
        _avl.Given(Request.Create().WithPath("/AVL/api/Login/Identity").UsingGet())
            .RespondWith(Response.Create().WithHeader("Content-Type", "application/json")
                .WithBody("""{"$id":"1","UserId":900,"Login":"demo","FirstName":"Jan","LastName":"Kowalski"}"""));

        Directory.CreateDirectory(_dir);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseEnvironment("Testing")
            .UseSetting("UniAvl:BaseUrl", _avl.Url + "/AVL/")
            .UseSetting("ConnectionStrings:Flota", $"Data Source={Path.Combine(_dir, "flota.db")};Pooling=False")
            .UseSetting("DataProtection:KeysPath", Path.Combine(_dir, "keys"))
            .UseSetting("Live:PollInterval", "01:00:00")
            .UseSetting("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/8")
            .ConfigureServices(s => s.AddTransient<IStartupFilter, RemoteIpFilter>()));
    }

    public void Dispose()
    {
        _factory.Dispose();
        _avl.Stop();
        _avl.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Adres klienta połączenia ustawiany z nagłówka testowego (TestServer nie podaje adresu) — przed obsługą nagłówków proxy.</summary>
    sealed class RemoteIpFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((ctx, nextMiddleware) =>
            {
                if (IPAddress.TryParse(ctx.Request.Headers[RemoteIpHeader], out var ip)) ctx.Connection.RemoteIpAddress = ip;
                return nextMiddleware(ctx);
            });
            next(app);
        };
    }

    /// <summary>Klient bez automatycznej obsługi ciasteczek — ciasteczka przekazywane jawnie (sprawdzenie atrybutów).</summary>
    HttpClient Client(string baseAddress = "http://localhost")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, BaseAddress = new Uri(baseAddress) });
        return client;
    }

    static HttpRequestMessage AuthRequest(string path, HttpContent? content, string? cookie = null, string? bearer = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        request.Headers.Add(AuthSession.SessionModeHeader, AuthSession.CookieMode);
        if (cookie is not null) request.Headers.Add("Cookie", $"{AuthSession.RefreshCookie}={cookie}");
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return request;
    }

    static HttpContent LoginBody() => JsonContent.Create(new LoginRequest("demo", "dobre"), FlotaJsonContext.Default.LoginRequest);

    static HttpContent EmptyRefresh() => JsonContent.Create(new RefreshRequest(string.Empty), FlotaJsonContext.Default.RefreshRequest);

    static string? SetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values.FirstOrDefault(v => v.StartsWith(AuthSession.RefreshCookie + "=", StringComparison.Ordinal)) : null;

    static string CookieValue(string setCookie) => setCookie[(AuthSession.RefreshCookie.Length + 1)..].Split(';')[0];

    static async Task<(TokenResponse Tokens, string Cookie)> LoginAsync(HttpClient client)
    {
        using var request = AuthRequest("/auth/login", LoginBody());
        using var response = await client.SendAsync(request, Ct);
        response.EnsureSuccessStatusCode();
        var tokens = (await response.Content.ReadFromJsonAsync(FlotaJsonContext.Default.TokenResponse, Ct))!;
        return (tokens, CookieValue(SetCookie(response)!));
    }

    [Fact]
    public async Task Login_CookieMode_HttpOnlyStrictCookie_NoRefreshTokenInBody()
    {
        using var client = Client();
        using var request = AuthRequest("/auth/login", LoginBody());
        using var response = await client.SendAsync(request, Ct);
        response.EnsureSuccessStatusCode();
        var tokens = (await response.Content.ReadFromJsonAsync(FlotaJsonContext.Default.TokenResponse, Ct))!;

        Assert.Equal(string.Empty, tokens.RefreshToken);
        Assert.False(string.IsNullOrEmpty(tokens.AccessToken));
        var cookie = SetCookie(response)!.ToLowerInvariant();
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/auth", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.Ordinal); // localhost — bezpieczny kontekst przeglądarki
    }

    [Fact]
    public async Task Login_WithoutCookieMode_TokenInBodyAndNoCookie()
    {
        using var client = Client();
        using var response = await client.PostAsync(new Uri("/auth/login", UriKind.Relative), LoginBody(), Ct);
        var tokens = (await response.Content.ReadFromJsonAsync(FlotaJsonContext.Default.TokenResponse, Ct))!;
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
        Assert.Null(SetCookie(response));
    }

    [Fact]
    public async Task Refresh_AfterReload_CookieOnly_RotatesCookie()
    {
        using var client = Client();
        var (_, cookie) = await LoginAsync(client);

        // Przeładowanie strony: brak tokenów w pamięci, wyłącznie ciasteczko
        using var request = AuthRequest("/auth/refresh", EmptyRefresh(), cookie);
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = (await response.Content.ReadFromJsonAsync(FlotaJsonContext.Default.TokenResponse, Ct))!;
        Assert.False(string.IsNullOrEmpty(tokens.AccessToken));
        Assert.Equal(string.Empty, tokens.RefreshToken);
        var rotated = CookieValue(SetCookie(response)!);
        Assert.NotEqual(cookie, rotated);

        // Nowy token dostępowy obsługiwany przez API
        using var settings = new HttpRequestMessage(HttpMethod.Get, "/me/settings");
        settings.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var settingsResponse = await client.SendAsync(settings, Ct);
        Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);

        // Poprzednie ciasteczko po rotacji odrzucone i usunięte
        using var reused = AuthRequest("/auth/refresh", EmptyRefresh(), cookie);
        using var reusedResponse = await client.SendAsync(reused, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, reusedResponse.StatusCode);
        Assert.Contains("expires=thu, 01 jan 1970", SetCookie(reusedResponse)!.ToLowerInvariant(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_CookieModeWithoutCookie_Unauthorized_EvenWithTokenInBody()
    {
        using var client = Client();
        using var plain = await client.PostAsync(new Uri("/auth/login", UriKind.Relative), LoginBody(), Ct);
        var bodyToken = (await plain.Content.ReadFromJsonAsync(FlotaJsonContext.Default.TokenResponse, Ct))!.RefreshToken;

        using var request = AuthRequest("/auth/refresh", JsonContent.Create(new RefreshRequest(bodyToken), FlotaJsonContext.Default.RefreshRequest));
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_DeletesCookieAndRevokesSession()
    {
        using var client = Client();
        var (tokens, cookie) = await LoginAsync(client);

        using var logout = AuthRequest("/auth/logout", null, cookie, tokens.AccessToken);
        using var logoutResponse = await client.SendAsync(logout, Ct);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Contains("expires=thu, 01 jan 1970", SetCookie(logoutResponse)!.ToLowerInvariant(), StringComparison.Ordinal);

        using var refresh = AuthRequest("/auth/refresh", EmptyRefresh(), cookie);
        using var refreshResponse = await client.SendAsync(refresh, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Theory]
    [InlineData("10.1.2.3", true)]      // proxy z sieci zaufanej (ForwardedHeaders:KnownNetworks)
    [InlineData("192.0.2.10", false)]   // nagłówek spoza sieci zaufanej — ignorowany
    public async Task ProxyHeaders_SecureCookieOnlyFromTrustedNetwork(string remoteIp, bool secure)
    {
        using var client = Client("http://flota.test");
        using var request = AuthRequest("/auth/login", LoginBody());
        request.Headers.Add(RemoteIpHeader, remoteIp);
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");
        using var response = await client.SendAsync(request, Ct);
        response.EnsureSuccessStatusCode();
        Assert.Equal(secure, SetCookie(response)!.ToLowerInvariant().Contains("secure", StringComparison.Ordinal));
    }
}
