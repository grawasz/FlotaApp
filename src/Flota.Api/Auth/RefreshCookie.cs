using Flota.Core.Contracts;
using Microsoft.Extensions.Options;

namespace Flota.Api.Auth;

/// <summary>
/// Token odświeżający w ciasteczku (wersja przeglądarkowa, T9.2): <c>HttpOnly</c>, <c>SameSite=Strict</c>, ścieżka <c>/auth</c>;
/// atrybut <c>Secure</c> przy żądaniu HTTPS (za odwrotnym proxy — z nagłówka <c>X-Forwarded-Proto</c> zaufanego proxy)
/// oraz dla adresu <c>localhost</c> (przeglądarki traktują go jako bezpieczny kontekst).
/// </summary>
public sealed class RefreshCookie(IOptions<AuthOptions> options)
{
    readonly AuthOptions _options = options.Value;

    /// <summary>Żądanie w trybie ciasteczkowym (nagłówek <see cref="AuthSession.SessionModeHeader"/>).</summary>
    public static bool IsCookieMode(HttpRequest request) =>
        string.Equals(request.Headers[AuthSession.SessionModeHeader], AuthSession.CookieMode, StringComparison.OrdinalIgnoreCase);

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(AuthSession.RefreshCookie, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    public void Write(HttpResponse response, string refreshToken) =>
        response.Cookies.Append(AuthSession.RefreshCookie, refreshToken, Options(response.HttpContext.Request, _options.SessionLifetime));

    public void Delete(HttpResponse response) =>
        response.Cookies.Delete(AuthSession.RefreshCookie, Options(response.HttpContext.Request, null));

    static CookieOptions Options(HttpRequest request, TimeSpan? maxAge) => new()
    {
        HttpOnly = true,
        Secure = request.IsHttps || string.Equals(request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase),
        SameSite = SameSiteMode.Strict,
        Path = AuthSession.CookiePath,
        MaxAge = maxAge,
        IsEssential = true,
    };
}
