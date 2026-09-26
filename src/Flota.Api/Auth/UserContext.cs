using System.Security.Claims;
using Flota.Api.Infrastructure.UniAvl;

namespace Flota.Api.Auth;

/// <summary>Kontekst bieżącego użytkownika w zakresie żądania (B2 — brak stanu współdzielonego między użytkownikami).</summary>
public sealed class UserContext(IHttpContextAccessor accessor, SessionStore sessions)
{
    ActiveSession? _session;

    public Guid SessionId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(TokenService.SessionClaim), out var id)
            ? id
            : throw new UniAvlUnauthorizedException();

    /// <summary>Sesja bieżącego żądania; brak sesji — 401.</summary>
    public async Task<ActiveSession> SessionAsync(CancellationToken ct) =>
        _session ??= await sessions.GetAsync(SessionId, ct) ?? throw new UniAvlUnauthorizedException();
}
