namespace Flota.App.Services;

/// <summary>
/// Znaczniki pomiaru wydajności (docs/WEB.md, tools/TestyUi/pomiar.mjs): wpis „[pomiar] nazwa” w konsoli przeglądarki,
/// odczytywany z czasem zdarzenia przez protokół CDP. Włączane parametrem adresu <c>?pomiar=1</c> (wersja przeglądarkowa);
/// bez parametru — brak działania. Zapis bezpośrednio przez <c>console.log</c> (<see cref="Sink"/>), niezależnie od dziennika Uno.
/// </summary>
public static class Pomiar
{
    /// <summary>Zapis wpisu; ustawiany w punkcie wejścia WebAssembly, gdy pomiar jest włączony.</summary>
    public static Action<string>? Sink { get; set; }

    public static void Mark(string name) => Sink?.Invoke($"[pomiar] {name}");

    /// <summary>Znacznik przy pierwszej klatce wyrenderowanej po wywołaniu.</summary>
    public static void MarkNextFrame(string name)
    {
        if (Sink is null) return;
        EventHandler<object>? handler = null;
        handler = (_, _) =>
        {
            CompositionTarget.Rendering -= handler;
            Mark(name);
        };
        CompositionTarget.Rendering += handler;
    }
}
