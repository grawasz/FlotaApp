using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Flota.Core.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Flota.Api.Tests;

/// <summary>Uwierzytelnianie Flota.Api z symulowanym UniAVL (WireMock) i tymczasową bazą SQLite.</summary>
public sealed class AuthIntegrationTests : IDisposable
{
    readonly WireMockServer _avl = WireMockServer.Start();
    readonly string _dir = Path.Combine(Path.GetTempPath(), "flota-api-tests-" + Guid.NewGuid().ToString("N"));
    readonly WebApplicationFactory<Program> _factory;

    public AuthIntegrationTests()
    {
        _avl.Given(Request.Create().WithPath("/AVL/api/Login/Token").WithParam("Login", "demo").WithParam("Password", "dobre").UsingGet())
            .RespondWith(Response.Create().WithBody("\"avl-token\""));
        _avl.Given(Request.Create().WithPath("/AVL/api/Login/Token").WithParam("Password", "zle").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Unauthorized));
        _avl.Given(Request.Create().WithPath("/AVL/api/Login/Identity").WithHeader("Authorization", "Token avl-token").UsingGet())
            .RespondWith(Response.Create().WithHeader("Content-Type", "application/json")
                .WithBody("""{"$id":"1","UserId":900,"Login":"demo","FirstName":"Jan","LastName":"Kowalski","Company":{"$id":"2","CompanyName":"Firma testowa"}}"""));

        Directory.CreateDirectory(_dir);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseEnvironment("Testing")
            .UseSetting("UniAvl:BaseUrl", _avl.Url + "/AVL/")
            .UseSetting("ConnectionStrings:Flota", $"Data Source={Path.Combine(_dir, "flota.db")};Pooling=False")
            .UseSetting("DataProtection:KeysPath", Path.Combine(_dir, "keys"))
            .UseSetting("Live:PollInterval", "01:00:00"));
    }

    public void Dispose()
    {
        _factory.Dispose();
        _avl.Stop();
        _avl.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    static async Task<TokenResponse> LoginAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("demo", "dobre"), FlotaJsonContext.Default.LoginRequest, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(FlotaJsonContext.Default.TokenResponse, Ct))!;
    }

    [Fact]
    public async Task Login_ReturnsTokensAndUser()
    {
        using var client = _factory.CreateClient();
        var tokens = await LoginAsync(client);
        Assert.False(string.IsNullOrEmpty(tokens.AccessToken));
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
        Assert.Equal(900, tokens.User.UserId);
        Assert.Equal("Firma testowa", tokens.User.CompanyName);
    }

    [Fact]
    public async Task Login_WrongPassword_Unauthorized()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("demo", "zle"), FlotaJsonContext.Default.LoginRequest, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_MissingPassword_BadRequest()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("demo", ""), FlotaJsonContext.Default.LoginRequest, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Unauthorized()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/me/settings", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesToken_OldTokenRejected()
    {
        using var client = _factory.CreateClient();
        var first = await LoginAsync(client);

        using var refreshed = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(first.RefreshToken), FlotaJsonContext.Default.RefreshRequest, Ct);
        refreshed.EnsureSuccessStatusCode();
        var second = (await refreshed.Content.ReadFromJsonAsync(FlotaJsonContext.Default.TokenResponse, Ct))!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        using var reused = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(first.RefreshToken), FlotaJsonContext.Default.RefreshRequest, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        using var client = _factory.CreateClient();
        var tokens = await LoginAsync(client);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var logoutResponse = await client.SendAsync(logout, Ct);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using var refresh = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(tokens.RefreshToken), FlotaJsonContext.Default.RefreshRequest, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task AvlSessionExpired_MapsTo401()
    {
        _avl.Given(Request.Create().WithPath("/AVL/api/TrackingObjects").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Unauthorized));
        using var client = _factory.CreateClient();
        var tokens = await LoginAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/fleet/snapshot");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
