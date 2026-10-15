using System.Net;
using System.Net.Http.Json;
using Flota.Api.Downloads;
using Flota.Core.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Flota.Api.Tests;

/// <summary>
/// Pobieranie pakietu Android (T9.5): metadane <c>/downloads/android</c>, plik pod <c>/pobierz/</c>, brak pliku, wersja z manifestu
/// pakietu i z konfiguracji. Próbka: pakiet z manifestem binarnym buildu testowego aplikacji (versionName 1.0).
/// </summary>
public sealed class DownloadsTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "flota-downloads-tests-" + Guid.NewGuid().ToString("N"));
    readonly string _downloads;

    public DownloadsTests()
    {
        _downloads = Path.Combine(_dir, "pobierz");
        Directory.CreateDirectory(_downloads);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    static string Sample => Path.Combine(AppContext.BaseDirectory, "Samples", "flota-test.apk");

    WebApplicationFactory<Program> Factory(params (string Key, string Value)[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing")
                .UseSetting("UniAvl:BaseUrl", "http://localhost:9/AVL/")
                .UseSetting("ConnectionStrings:Flota", $"Data Source={Path.Combine(_dir, "flota.db")};Pooling=False")
                .UseSetting("DataProtection:KeysPath", Path.Combine(_dir, "keys"))
                .UseSetting("Live:PollInterval", "01:00:00")
                .UseSetting("Downloads:Path", _downloads);
            foreach (var (key, value) in settings) b.UseSetting(key, value);
        });

    [Fact]
    public void ApkManifest_ReadsVersionName() => Assert.Equal("1.0", ApkManifest.ReadVersionName(Sample));

    [Fact]
    public void ApkManifest_NotAPackage_Null()
    {
        var path = Path.Combine(_dir, "nie-apk.apk");
        File.WriteAllText(path, "tekst");
        Assert.Null(ApkManifest.ReadVersionName(path));
    }

    [Fact]
    public async Task Metadata_And_Download()
    {
        File.Copy(Sample, Path.Combine(_downloads, "flota.apk"));
        using var factory = Factory();
        using var client = factory.CreateClient();

        var info = await client.GetFromJsonAsync(new Uri("/downloads/android", UriKind.Relative), FlotaJsonContext.Default.AppDownloadInfo, Ct);
        Assert.NotNull(info);
        Assert.Equal("flota.apk", info.FileName);
        Assert.Equal("pobierz/flota.apk", info.Url);
        Assert.Equal(new FileInfo(Sample).Length, info.Size);
        Assert.Equal("1.0", info.Version);

        using var response = await client.GetAsync(new Uri("/" + info.Url, UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.android.package-archive", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoCache);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(info.Size, (await response.Content.ReadAsByteArrayAsync(Ct)).Length);
    }

    [Fact]
    public async Task VersionFromConfiguration_TakesPrecedence()
    {
        File.Copy(Sample, Path.Combine(_downloads, "flota.apk"));
        using var factory = Factory(("Downloads:AndroidVersion", "2.3"));
        using var client = factory.CreateClient();
        var info = await client.GetFromJsonAsync(new Uri("/downloads/android", UriKind.Relative), FlotaJsonContext.Default.AppDownloadInfo, Ct);
        Assert.Equal("2.3", info!.Version);
    }

    [Fact]
    public async Task NoPackage_NotFound()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var metadata = await client.GetAsync(new Uri("/downloads/android", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NotFound, metadata.StatusCode);
        using var file = await client.GetAsync(new Uri("/pobierz/flota.apk", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NotFound, file.StatusCode);
    }

    [Fact]
    public async Task OtherFileTypes_NotServed()
    {
        File.WriteAllText(Path.Combine(_downloads, "notatka.txt"), "tekst");
        using var factory = Factory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/pobierz/notatka.txt", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
