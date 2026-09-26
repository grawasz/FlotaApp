using System.Net;
using Flota.Api.Infrastructure.UniAvl;
using Microsoft.Extensions.Logging.Abstractions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Flota.Api.Tests;

public sealed class UniAvlClientTests : IDisposable
{
    const string Token = "Token test-token";
    readonly WireMockServer _server = WireMockServer.Start();
    readonly HttpClient _http;
    readonly UniAvlClient _client;

    public UniAvlClientTests()
    {
        _http = new HttpClient { BaseAddress = new Uri(_server.Url! + "/AVL/") };
        _client = new UniAvlClient(_http, NullLogger<UniAvlClient>.Instance);
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Stop();
        _server.Dispose();
    }

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_ReturnsPrefixedToken()
    {
        _server.Given(Request.Create().WithPath("/AVL/api/Login/Token").WithParam("Login", "demo").UsingGet())
            .RespondWith(Response.Create().WithBody("\"abc\""));
        Assert.Equal("Token abc", await _client.LoginAsync("demo", "x", Ct));
    }

    [Fact]
    public async Task Login_Unauthorized_ReturnsNull()
    {
        _server.Given(Request.Create().WithPath("/AVL/api/Login/Token").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Unauthorized));
        Assert.Null(await _client.LoginAsync("demo", "x", Ct));
    }

    [Fact]
    public async Task Get_SendsTokenHeader_AndDeserializes()
    {
        _server.Given(Request.Create().WithPath("/AVL/api/TrackingObjects").WithHeader("Authorization", Token).UsingGet())
            .RespondWith(Response.Create().WithHeader("Content-Type", "application/json").WithBody(Samples.Text("TrackingObjects")));
        var list = await _client.GetTrackingObjectsAsync(Token, Ct);
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task Get_Unauthorized_Throws()
    {
        _server.Given(Request.Create().WithPath("/AVL/api/TrackingMessages/Last").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Unauthorized));
        await Assert.ThrowsAsync<UniAvlUnauthorizedException>(() => _client.GetLastMessagesAsync(Token, Ct));
    }

    [Fact]
    public async Task Get_ServerError_ThrowsWithStatus()
    {
        _server.Given(Request.Create().WithPath("/AVL/api/TrackingMessages/Last").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));
        var ex = await Assert.ThrowsAsync<UniAvlException>(() => _client.GetLastMessagesAsync(Token, Ct));
        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    [Fact]
    public async Task Get_InvalidJson_Throws()
    {
        _server.Given(Request.Create().WithPath("/AVL/api/TrackingObjects").UsingGet())
            .RespondWith(Response.Create().WithHeader("Content-Type", "application/json").WithBody("<html>"));
        await Assert.ThrowsAsync<UniAvlException>(() => _client.GetTrackingObjectsAsync(Token, Ct));
    }

    [Fact]
    public async Task Get_NoContent_ReturnsNull()
    {
        _server.Given(Request.Create().WithPath("/AVL/api/TrackingObjects").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.NoContent));
        Assert.Null(await _client.GetTrackingObjectsAsync(Token, Ct));
    }

    [Fact]
    public async Task Route_QueryIsCultureInvariant()
    {
        _server.Given(Request.Create().WithPath("/AVL/api/TrackingObjects/101/TrackingMessages/Route").UsingGet())
            .RespondWith(Response.Create().WithHeader("Content-Type", "application/json").WithBody(Samples.Text("Route")));

        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pl-PL");
        try
        {
            var route = await _client.GetRouteAsync(Token, 101, new DateTime(2026, 9, 25, 6, 0, 0), new DateTime(2026, 9, 25, 18, 0, 0),
                TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(1), 2.5m, false, Ct);
            Assert.NotNull(route);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }

        var query = Assert.Single(_server.LogEntries).RequestMessage!.Query!;
        Assert.Equal("2026-09-25 06:00:00", query["from"].ToString());
        Assert.Equal("2.5", query["speed"].ToString());
        Assert.Equal("00:03:00", query["timeStop"].ToString());
    }
}
