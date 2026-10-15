using Flota.Api.Auth;
using Flota.Api.Infrastructure.Export;
using Flota.Api.Infrastructure.UniAvl;
using Flota.Api.Services;
using Flota.Core.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Flota.Api.Endpoints;

/// <summary>Punkty końcowe Flota.Api (plan, 5.2).</summary>
public static class ApiEndpoints
{
    const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static void MapFlotaApi(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth").WithTags("Uwierzytelnianie");
        auth.MapPost("/login", LoginAsync).AllowAnonymous();
        auth.MapPost("/refresh", RefreshAsync).AllowAnonymous();
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization();

        var api = app.MapGroup("").RequireAuthorization();

        api.MapGet("/fleet/snapshot", async (UserContext user, FleetService fleet, CancellationToken ct) =>
            TypedResults.Ok(await fleet.SnapshotAsync(await user.SessionAsync(ct), ct))).WithTags("Flota");

        api.MapGet("/me/settings", async (UserContext user, SettingsService settings, CancellationToken ct) =>
            TypedResults.Ok(await settings.GetAsync((await user.SessionAsync(ct)).UserId, ct))).WithTags("Ustawienia");
        api.MapPut("/me/settings", async (UserSettings body, UserContext user, SettingsService settings, CancellationToken ct) =>
        {
            await settings.SaveAsync((await user.SessionAsync(ct)).UserId, body, ct);
            return TypedResults.NoContent();
        }).WithTags("Ustawienia");

        var objects = api.MapGroup("/objects/{id:int}").WithTags("Raport trasy");
        objects.MapGet("/route", async Task<Results<Ok<RouteReport>, NotFound>> (int id, [AsParameters] RouteParameters p, UserContext user, ReportService reports, CancellationToken ct) =>
            await reports.RouteAsync(await user.SessionAsync(ct), id, p.ToQuery(), ct) is { } r ? TypedResults.Ok(r) : TypedResults.NotFound());
        objects.MapPost("/waypoints", async (int id, WaypointsRequest body, UserContext user, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.WaypointsAsync(await user.SessionAsync(ct), id, body.Stages, ct)));
        objects.MapGet("/trace", async (int id, DateTime from, DateTime to, UserContext user, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.TraceAsync(await user.SessionAsync(ct), id, from, to, ct)));
        objects.MapGet("/fuel", async Task<Results<Ok<FuelReport>, NotFound>> (int id, DateTime from, DateTime to, UserContext user, ReportService reports, CancellationToken ct) =>
            await reports.FuelAsync(await user.SessionAsync(ct), id, from, to, ct) is { } r ? TypedResults.Ok(r) : TypedResults.NotFound());
        objects.MapGet("/events", async (int id, DateTime from, DateTime to, UserContext user, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.EventsAsync(await user.SessionAsync(ct), id, from, to, ct)));
        objects.MapGet("/driving-style", async Task<Results<Ok<DrivingStyle>, NotFound>> (int id, [AsParameters] RouteParameters p,
            decimal? speeding, decimal? exceedRpm, decimal? suddenBraking, decimal? quickAcceleration,
            UserContext user, ReportService reports, CancellationToken ct) =>
        {
            var q = new DrivingStyleQuery
            {
                Route = p.ToQuery(),
                Speeding = speeding ?? 50,
                ExceedRpm = exceedRpm ?? 2000,
                SuddenBraking = suddenBraking ?? 1,
                QuickAcceleration = quickAcceleration ?? 1,
            };
            return await reports.DrivingStyleAsync(await user.SessionAsync(ct), id, q, ct) is { } r ? TypedResults.Ok(r) : TypedResults.NotFound();
        });
        objects.MapGet("/stage-summary", async (int id, [AsParameters] RouteParameters p, UserContext user, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.StageSummaryAsync(await user.SessionAsync(ct), id, p.ToQuery(), ct)));

        var summary = api.MapGroup("/reports").WithTags("Raporty zbiorcze");
        summary.MapPost("/fleet", async (FleetReportRequest body, UserContext user, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.FleetReportAsync(await user.SessionAsync(ct), body, ct)));
        summary.MapPost("/eco-driving", async (EcoDrivingRequest body, UserContext user, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.EcoDrivingAsync(await user.SessionAsync(ct), body, ct)));

        var logbooks = api.MapGroup("/logbooks").WithTags("Karty drogowe");
        logbooks.MapGet("", async (DateTime from, DateTime to, UserContext user, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.LogbooksAsync(await user.SessionAsync(ct), from, to, ct)));
        logbooks.MapGet("/{id:int}", async (int id, UserContext user, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.LogbookEntriesAsync(await user.SessionAsync(ct), id, ct)));

        var dict = api.MapGroup("/dictionaries").WithTags("Słowniki");
        dict.MapGet("/drivers", async (UserContext user, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.DriversAsync(await user.SessionAsync(ct), ct)));
        dict.MapGet("/equipment", async (bool? refresh, UserContext user, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.EquipmentAsync(await user.SessionAsync(ct), refresh ?? false, ct)));
        dict.MapPost("/event-templates", async (EventTemplatesRequest body, UserContext user, ReportService reports, CancellationToken ct) =>
            TypedResults.Ok(await reports.EventTemplatesAsync(await user.SessionAsync(ct), body.ObjectIds, ct)));

        api.MapPost("/export/xlsx", (ExportTable table, TimeProvider time) =>
            TypedResults.File(XlsxExporter.Export(table, time.GetLocalNow()), XlsxContentType)).WithTags("Eksport");
    }

    static async Task<Results<Ok<TokenResponse>, UnauthorizedHttpResult, BadRequest<ErrorResponse>>> LoginAsync(
        LoginRequest body, HttpContext http, UniAvlClient avl, SessionStore sessions, TokenService tokens, RefreshCookie cookie, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Login) || string.IsNullOrEmpty(body.Password))
            return TypedResults.BadRequest(new ErrorResponse("Wprowadź login i hasło."));
        var avlToken = await avl.LoginAsync(body.Login.Trim(), body.Password, ct);
        if (avlToken is null) return TypedResults.Unauthorized();

        var identity = await avl.GetIdentityAsync(avlToken, ct);
        var user = UniAvlMapper.ToUser(identity ?? new Infrastructure.UniAvl.Dto.AvlIdentity(), body.Login.Trim());
        var (session, refresh) = await sessions.CreateAsync(user.UserId, user.Login, avlToken, ct);
        var (access, expires) = tokens.Issue(session);
        return TypedResults.Ok(new TokenResponse(access, IssueRefresh(http, cookie, refresh), expires, user));
    }

    /// <summary>
    /// Odnowienie tokenu: token odświeżający z treści żądania albo — w trybie ciasteczkowym — wyłącznie z ciasteczka.
    /// </summary>
    static async Task<Results<Ok<TokenResponse>, UnauthorizedHttpResult>> RefreshAsync(
        RefreshRequest? body, HttpContext http, UniAvlClient avl, SessionStore sessions, TokenService tokens, RefreshCookie cookie, CancellationToken ct)
    {
        var presented = RefreshCookie.IsCookieMode(http.Request) ? RefreshCookie.Read(http.Request) : body?.RefreshToken;
        if (string.IsNullOrEmpty(presented)) return TypedResults.Unauthorized();
        var result = await sessions.RefreshAsync(presented, ct);
        if (result is null)
        {
            if (RefreshCookie.IsCookieMode(http.Request)) cookie.Delete(http.Response);
            return TypedResults.Unauthorized();
        }
        var (session, refresh) = result.Value;
        var identity = await avl.GetIdentityAsync(session.AvlToken, ct);
        var (access, expires) = tokens.Issue(session);
        var user = UniAvlMapper.ToUser(identity ?? new Infrastructure.UniAvl.Dto.AvlIdentity { UserId = session.UserId }, session.Login);
        return TypedResults.Ok(new TokenResponse(access, IssueRefresh(http, cookie, refresh), expires, user));
    }

    static async Task<NoContent> LogoutAsync(HttpContext http, UserContext user, SessionStore sessions, RefreshCookie cookie, CancellationToken ct)
    {
        await sessions.RevokeAsync(user.SessionId, ct);
        if (RefreshCookie.IsCookieMode(http.Request)) cookie.Delete(http.Response);
        return TypedResults.NoContent();
    }

    /// <summary>Token odświeżający dla klienta: w trybie ciasteczkowym w ciasteczku (treść odpowiedzi — pusty ciąg).</summary>
    static string IssueRefresh(HttpContext http, RefreshCookie cookie, string refresh)
    {
        if (!RefreshCookie.IsCookieMode(http.Request)) return refresh;
        cookie.Write(http.Response, refresh);
        return string.Empty;
    }

    /// <summary>Parametry raportu trasy w zapytaniu.</summary>
    public sealed record RouteParameters(
        DateTime From,
        DateTime To,
        TimeSpan? StopTime,
        TimeSpan? IgnitionOffTime,
        decimal? MinSpeed,
        bool? StageSubtypeBySensor)
    {
        public RouteQuery ToQuery() => new()
        {
            From = From,
            To = To,
            StopTime = StopTime,
            IgnitionOffTime = IgnitionOffTime,
            MinSpeed = MinSpeed ?? 0,
            StageSubtypeBySensor = StageSubtypeBySensor ?? true,
        };
    }
}
