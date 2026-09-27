using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Serialization;
using Flota.Api.Infrastructure.UniAvl.Dto;

namespace Flota.UniAvlSim;

/// <summary>
/// Symulator UniAVL REST API: flota fikcyjna, ruch obiektów wyznaczany z bieżącego czasu.
/// Przeznaczenie: uruchamianie i testowanie Flota.Api oraz aplikacji bez konta UniAVL.
/// </summary>
internal static class SimApp
{
    public static Task Main(string[] args) => Create(args).RunAsync();

    /// <summary>Aplikacja symulatora; <paramref name="configure"/> — zmiany konfiguracji (testy: serwer testowy, czas).</summary>
    public static WebApplication Create(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<SimData>();
        configure?.Invoke(builder);

        var app = builder.Build();
        var login = app.Configuration["Sim:Login"] ?? "demo";
        var password = app.Configuration["Sim:Password"] ?? "demo";
        var tokens = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        var json = SimJsonContext.Default;

        var avl = app.MapGroup("/AVL/api");

        avl.MapGet("/Login/Token", (string? Login, string? Password) =>
        {
            if (Login != login || Password != password) return Results.Unauthorized();
            var token = "sim-" + Guid.NewGuid().ToString("N");
            tokens[token] = Login;
            return Results.Json(token, json.String);
        });

        // Pozostałe wywołania wymagają nagłówka „Authorization: Token …” z tokenem wydanym przez symulator
        var api = avl.MapGroup("").AddEndpointFilter(async (ctx, next) =>
        {
            var header = ctx.HttpContext.Request.Headers.Authorization.ToString();
            return header.StartsWith("Token ", StringComparison.Ordinal) && tokens.ContainsKey(header[6..])
                ? await next(ctx)
                : Results.Unauthorized();
        });

        api.MapGet("/Login/Identity", (HttpRequest r) =>
            Results.Json(SimData.Identity(tokens[r.Headers.Authorization.ToString()[6..]]), json.AvlIdentity));
        api.MapGet("/TrackingObjects", () => Results.Json(SimData.TrackingObjects(), json.ListAvlTrackingObject));
        api.MapGet("/TrackingMessages/Last", (SimData d) => Results.Json(d.LastMessages(), json.ListAvlTrackingMessage));
        api.MapGet("/Areas", () => Results.Json(SimData.Areas(), json.ListAvlArea));
        api.MapGet("/Users/Drivers", () => Results.Json(SimData.Drivers(), json.ListAvlDriver));
        api.MapGet("/Equipment", () => Results.Json(SimData.Equipment(), json.ListAvlEquipment));

        var objects = api.MapGroup("/TrackingObjects/{id:int}");
        objects.MapGet("/TrackingMessages", (int id, string from, string to, SimData d) =>
            WithObject(id, o => Results.Json(d.Messages(o, Date(from), Date(to)), json.ListAvlTrackingMessage)));
        objects.MapGet("/TrackingMessages/Route", (int id, string from, string to, SimData d) =>
            WithObject(id, o => Results.Json(d.Route(o, Date(from), Date(to)), json.AvlRoute)));
        objects.MapGet("/FuelReport", (int id, string from, string to, SimData d) =>
            WithObject(id, o => d.FuelReport(o, Date(from), Date(to)) is { } f ? Results.Json(f, json.AvlFuelReport) : Results.NoContent()));
        objects.MapGet("/Events", (int id, string From, string To, SimData d) =>
            WithObject(id, o => Results.Json(d.Events(o, Date(From), Date(To)), json.ListAvlEvent)));
        objects.MapGet("/EventTemplates", (int id) =>
            WithObject(id, o => Results.Json(SimData.EventTemplates(o), json.ListAvlEventTemplate)));
        objects.MapGet("/DrivingStyle", (int id, string from, string to, SimData d) =>
            WithObject(id, o => Results.Json(d.DrivingStyle(o, Date(from), Date(to)), json.AvlDrivingStyle)));
        objects.MapPost("/FleetReport", (int id, string From, string To, SimData d) =>
            WithObject(id, o => Results.Json(d.FleetReport(o, Date(From), Date(To)), json.AvlFleetReport)));

        api.MapGet("/Logbooks", (string from, string to, SimData d) => Results.Json(d.Logbooks(Date(from), Date(to)), json.ListAvlLogbook));
        api.MapGet("/Logbooks/{id:int}/Details", (int id, SimData d) => Results.Json(d.LogbookDetails(id), json.ListAvlLogbookDetails));

        app.MapGet("/", () => "Symulator UniAVL — adres bazowy /AVL/");

        return app;
    }

    static IResult WithObject(int id, Func<SimObject, IResult> action) =>
        SimFleet.Find(id) is { } o ? action(o) : Results.NotFound();

    static DateTime Date(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces);
}

/// <summary>Serializacja kontraktu UniAVL (nazwy właściwości PascalCase jak w UniAVL).</summary>
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(AvlIdentity))]
[JsonSerializable(typeof(List<AvlTrackingObject>))]
[JsonSerializable(typeof(List<AvlTrackingMessage>))]
[JsonSerializable(typeof(List<AvlArea>))]
[JsonSerializable(typeof(List<AvlDriver>))]
[JsonSerializable(typeof(List<AvlEquipment>))]
[JsonSerializable(typeof(AvlRoute))]
[JsonSerializable(typeof(AvlFuelReport))]
[JsonSerializable(typeof(List<AvlEvent>))]
[JsonSerializable(typeof(List<AvlEventTemplate>))]
[JsonSerializable(typeof(AvlDrivingStyle))]
[JsonSerializable(typeof(AvlFleetReport))]
[JsonSerializable(typeof(List<AvlLogbook>))]
[JsonSerializable(typeof(List<AvlLogbookDetails>))]
internal sealed partial class SimJsonContext : JsonSerializerContext;
