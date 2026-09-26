namespace Flota.Core.Contracts;

/// <summary>Żądanie logowania (dane uwierzytelniające UniAVL użytkownika).</summary>
public sealed record LoginRequest(string Login, string Password);

/// <summary>Żądanie odnowienia tokenu dostępowego.</summary>
public sealed record RefreshRequest(string RefreshToken);

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
