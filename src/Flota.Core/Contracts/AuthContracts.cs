namespace Flota.Core.Contracts;

/// <summary>Żądanie logowania (dane uwierzytelniające UniAVL użytkownika).</summary>
public sealed record LoginRequest(string Login, string Password);

/// <summary>Żądanie odnowienia tokenu dostępowego (w trybie ciasteczkowym token pusty — odczyt z ciasteczka).</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>
/// Tryb sesji w przeglądarce: nagłówek <see cref="SessionModeHeader"/> o wartości <see cref="CookieMode"/> w żądaniach
/// <c>/auth/*</c> — token odświeżający w ciasteczku <c>HttpOnly</c> zamiast w treści odpowiedzi.
/// </summary>
public static class AuthSession
{
    public const string SessionModeHeader = "X-Flota-Session";
    public const string CookieMode = "cookie";
    public const string RefreshCookie = "flota_refresh";
    public const string CookiePath = "/auth";
}

/// <summary>Tokeny wydane przez Flota.Api.</summary>
public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    UserInfo User);

/// <summary>Zalogowany użytkownik.</summary>
public sealed record UserInfo(
    int UserId,
    string Login,
    string FirstName,
    string LastName,
    string? CompanyName)
{
    public string DisplayName => $"{FirstName} {LastName}".Trim();
}

/// <summary>Treść odpowiedzi z błędem (bez szczegółów technicznych).</summary>
public sealed record ErrorResponse(string Message);
