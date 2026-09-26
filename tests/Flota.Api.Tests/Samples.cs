using System.Text.Json;
using Flota.Api.Infrastructure.UniAvl.Dto;

namespace Flota.Api.Tests;

/// <summary>Próbki odpowiedzi UniAVL przygotowane ręcznie (dane fikcyjne) w formacie Newtonsoft z <c>$id</c>/<c>$ref</c>/<c>$values</c>.</summary>
static class Samples
{
    public static string Text(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", name + ".json"));

    public static List<AvlTrackingObject> TrackingObjects() =>
        JsonSerializer.Deserialize(Text("TrackingObjects"), UniAvlJsonContext.Default.ListAvlTrackingObject)!;

    public static List<AvlTrackingMessage> LastMessages() =>
        JsonSerializer.Deserialize(Text("LastMessages"), UniAvlJsonContext.Default.ListAvlTrackingMessage)!;

    public static AvlRoute Route() =>
        JsonSerializer.Deserialize(Text("Route"), UniAvlJsonContext.Default.AvlRoute)!;

    public static AvlFleetReport FleetReport() =>
        JsonSerializer.Deserialize(Text("FleetReport"), UniAvlJsonContext.Default.AvlFleetReport)!;
}
