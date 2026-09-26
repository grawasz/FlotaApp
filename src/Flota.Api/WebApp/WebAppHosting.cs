using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace Flota.Api.WebApp;

/// <summary>
/// Udostępnianie aplikacji przeglądarkowej (Uno WebAssembly) pod tym samym adresem co API:
/// pliki wstępnie skompresowane (<c>.br</c>, <c>.gz</c>), pamięć podręczna plików z sumą kontrolną w nazwie,
/// <c>index.html</c> dla ścieżek spoza API.
/// </summary>
public static class WebAppHosting
{
    public const string Section = "WebApp:RootPath";
    const string OriginalContentType = "flota.webapp.content-type";

    public static bool TryUseFlotaWebApp(this WebApplication app)
    {
        var root = app.Configuration[Section];
        if (string.IsNullOrWhiteSpace(root) || !File.Exists(Path.Combine(root, "index.html")))
            return false;

        var files = new PhysicalFileProvider(Path.GetFullPath(root));
        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".wasm"] = "application/wasm";
        types.Mappings[".dat"] = "application/octet-stream";
        types.Mappings[".blat"] = "application/octet-stream";
        types.Mappings[".webmanifest"] = "application/manifest+json";
        // Warianty skompresowane: typ zawartości zastępowany typem pliku źródłowego (OnPrepareResponse)
        var encoded = new FileExtensionContentTypeProvider(new Dictionary<string, string>(types.Mappings, StringComparer.OrdinalIgnoreCase)
        {
            [".br"] = "application/octet-stream",
            [".gz"] = "application/octet-stream",
        });

        var options = new StaticFileOptions
        {
            FileProvider = files,
            ContentTypeProvider = encoded,
            OnPrepareResponse = ctx =>
            {
                var headers = ctx.Context.Response.Headers;
                if (ctx.Context.Items[OriginalContentType] is string type)
                    headers.ContentType = type;
                var path = ctx.Context.Request.Path.Value ?? string.Empty;
                // Pliki z sumą kontrolną w nazwie lub ścieżce — niezmienne; pozostałe — weryfikacja przy każdym użyciu
                headers.CacheControl = path.StartsWith("/package_", StringComparison.Ordinal) || path.StartsWith("/_framework/", StringComparison.Ordinal)
                    ? "public, max-age=31536000, immutable"
                    : "no-cache";
            },
        };

        // Wersja wstępnie skompresowana zamiast kompresji w locie
        app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path.Value;
            if (HttpMethods.IsGet(ctx.Request.Method) && Path.HasExtension(path)
                && types.TryGetContentType(path, out var contentType))
            {
                var accept = ctx.Request.Headers.AcceptEncoding.ToString();
                foreach (var (encoding, extension) in new[] { ("br", ".br"), ("gzip", ".gz") })
                {
                    if (!accept.Contains(encoding, StringComparison.OrdinalIgnoreCase) || !files.GetFileInfo(path + extension).Exists)
                        continue;
                    ctx.Request.Path = path + extension;
                    ctx.Items[OriginalContentType] = contentType;
                    ctx.Response.Headers.ContentEncoding = encoding;
                    ctx.Response.Headers.Append(HeaderNames.Vary, HeaderNames.AcceptEncoding);
                    break;
                }
            }
            await next(ctx);
        });

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(options);
        app.MapFallbackToFile("index.html", options);
        return true;
    }
}
