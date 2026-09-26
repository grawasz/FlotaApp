using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Flota.Api.Auth;

/// <summary>Wydawanie tokenów dostępowych JWT.</summary>
public sealed class TokenService
{
    public const string SessionClaim = "sid";

    readonly AuthOptions _options;
    readonly TimeProvider _time;

    public TokenService(IOptions<AuthOptions> options, TimeProvider time)
    {
        _options = options.Value;
        _time = time;
        SigningKey = new SymmetricSecurityKey(string.IsNullOrWhiteSpace(_options.SigningKey)
            ? RandomNumberGenerator.GetBytes(32)
            : Convert.FromBase64String(_options.SigningKey));
    }

    public SymmetricSecurityKey SigningKey { get; }

    public (string Token, DateTimeOffset ExpiresAt) Issue(ActiveSession session)
    {
        var now = _time.GetUtcNow();
        var expires = now + _options.AccessTokenLifetime;
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, session.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Name, session.Login),
                new Claim(SessionClaim, session.Id.ToString()),
            ],
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidIssuer = _options.Issuer,
        ValidAudience = _options.Audience,
        IssuerSigningKey = SigningKey,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = JwtRegisteredClaimNames.Name,
    };
}
