using System.Collections.Concurrent;
using Flota.Core.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace Flota.Api.Downloads;

/// <summary>
/// Pliki do pobrania (T9.5): katalog <c>Downloads:Path</c> udostępniany pod <c>/pobierz/</c> (pakiety APK, nazwy stałe — bez pamięci
/// podręcznej) i metadane pakietu Android pod <c>GET /downloads/android</c>. Plik pakietu: <c>Downloads:AndroidFile</c> albo
/// najnowszy plik <c>*.apk</c> w katalogu; wersja: <c>Downloads:AndroidVersion</c> albo <c>versionName</c> z manifestu pakietu.
/// Pakiety publiczne — nie zawierają danych uwierzytelniających (CLAUDE.md, Bezpieczeństwo).
/// </summary>
public static class DownloadsHosting
{
    public const string Section = "Downloads";
    public const string RequestPath = "/pobierz";
    const string ApkContentType = "application/vnd.android.package-archive";
    static readonly ConcurrentDictionary<(string Path, DateTime Modified), string?> Versions = new();

    public static void UseFlotaDownloads(this WebApplication app)
    {
        var root = app.Configuration[$"{Section}:Path"];
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return;

        var types = new FileExtensionContentTypeProvider(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".apk"] = ApkContentType,
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.GetFullPath(root)),
            RequestPath = RequestPath,
            ContentTypeProvider = types,
            OnPrepareResponse = ctx =>
            {
                // Nazwa stała przy nowych wydaniach — zawsze weryfikacja u źródła
                ctx.Context.Response.Headers.CacheControl = "no-cache";
                ctx.Context.Response.Headers[HeaderNames.ContentDisposition] =
                    new ContentDispositionHeaderValue("attachment") { FileNameStar = ctx.File.Name }.ToString();
            },
        });
    }

    public static void MapFlotaDownloads(this IEndpointRouteBuilder app) =>
        app.MapGet("/downloads/android", Results<Ok<AppDownloadInfo>, NotFound> (IConfiguration configuration) =>
                Android(configuration) is { } info ? TypedResults.Ok(info) : TypedResults.NotFound())
            .AllowAnonymous()
            .WithTags("Pobieranie");

    /// <summary>Metadane pakietu Android; <c>null</c>, gdy katalog nie jest skonfigurowany albo brak pakietu.</summary>
    public static AppDownloadInfo? Android(IConfiguration configuration)
    {
        var root = configuration[$"{Section}:Path"];
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return null;
        var configured = configuration[$"{Section}:AndroidFile"];
        var file = !string.IsNullOrWhiteSpace(configured)
            ? new FileInfo(Path.Combine(root, Path.GetFileName(configured)))
            : new DirectoryInfo(root).EnumerateFiles("*.apk").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
        if (file is null || !file.Exists) return null;

        var version = configuration[$"{Section}:AndroidVersion"];
        if (string.IsNullOrWhiteSpace(version))
            version = Versions.GetOrAdd((file.FullName, file.LastWriteTimeUtc), key => ApkManifest.ReadVersionName(key.Path));
        return new AppDownloadInfo(file.Name, $"{RequestPath.TrimStart('/')}/{Uri.EscapeDataString(file.Name)}", file.Length,
            new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero), string.IsNullOrWhiteSpace(version) ? null : version);
    }
}
