using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Flota.Api.Tests;

/// <summary>Udostępnianie aplikacji przeglądarkowej przez Flota.Api (katalog tymczasowy z plikami próbnymi).</summary>
public sealed class WebAppHostingTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "flota-webapp-tests-" + Guid.NewGuid().ToString("N"));
    readonly WebApplicationFactory<Program> _factory;

    public WebAppHostingTests()
    {
        var root = Path.Combine(_dir, "wwwroot");
        Directory.CreateDirectory(Path.Combine(root, "_framework"));
        File.WriteAllText(Path.Combine(root, "index.html"), "<html>Flota</html>");
        File.WriteAllBytes(Path.Combine(root, "_framework", "app.abc.wasm"), [0, 97, 115, 109]);
        File.WriteAllBytes(Path.Combine(root, "_framework", "app.abc.wasm.br"), [1, 2, 3]);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseEnvironment("Testing")
            .UseSetting("UniAvl:BaseUrl", "http://localhost:9/AVL/")
            .UseSetting("ConnectionStrings:Flota", $"Data Source={Path.Combine(_dir, "flota.db")};Pooling=False")
            .UseSetting("DataProtection:KeysPath", Path.Combine(_dir, "keys"))
            .UseSetting("Live:PollInterval", "01:00:00")
            .UseSetting("WebApp:RootPath", root));
    }

    public void Dispose()
    {
        _factory.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Root_ServesIndexWithoutCaching()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task Framework_ServesBrotliVariantWithOriginalType()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_framework/app.abc.wasm");
        request.Headers.AcceptEncoding.ParseAdd("br");
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/wasm", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("br", response.Content.Headers.ContentEncoding);
        Assert.Equal([1, 2, 3], await response.Content.ReadAsByteArrayAsync(Ct));
        Assert.True(response.Headers.CacheControl?.Public);
    }

    [Fact]
    public async Task Framework_WithoutBrotli_ServesOriginal()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_framework/app.abc.wasm");
        request.Headers.AcceptEncoding.ParseAdd("identity");
        using var response = await client.SendAsync(request, Ct);
        Assert.Empty(response.Content.Headers.ContentEncoding);
        Assert.Equal(4, (await response.Content.ReadAsByteArrayAsync(Ct)).Length);
    }

    [Fact]
    public async Task UnknownPath_FallsBackToIndex()
    {
        using var client = _factory.CreateClient();
        var body = await client.GetStringAsync(new Uri("/raporty/trasa", UriKind.Relative), Ct);
        Assert.Contains("Flota", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiRoute_NotReplacedByIndex()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/me/settings", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
