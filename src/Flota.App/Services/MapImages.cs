using Flota.Core.Objects;
using Windows.Storage;

namespace Flota.App.Services;

/// <summary>Obrazy dla warstw Mapsui (ikony typów obiektów, strzałki kierunku) — źródła <c>base64-content://</c>.</summary>
public sealed class MapImages
{
    readonly Dictionary<string, string> _sources = new(StringComparer.Ordinal);

    public bool IsLoaded { get; private set; }

    public async Task LoadAsync()
    {
        if (IsLoaded) return;
        var names = Enumerable.Range(0, 23).Select(TrackingObjectIcons.IconKey).Distinct()
            .Select(k => $"{k}.png").ToList();
        // Odczyt równoległy: w przeglądarce każdy plik to osobne żądanie HTTP
        var sources = await Task.WhenAll(names.Select(LoadSourceAsync));
        for (var i = 0; i < names.Count; i++)
            if (sources[i] is { } source) _sources[names[i]] = source;
        IsLoaded = true;
    }

    static async Task<string?> LoadSourceAsync(string name)
    {
        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/Images/{name}"));
            var buffer = await FileIO.ReadBufferAsync(file);
            var bytes = new byte[buffer.Length];
            using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(buffer)) reader.ReadBytes(bytes);
            return "base64-content://" + Convert.ToBase64String(bytes);
        }
        catch (Exception ex) when (ex is FileNotFoundException or IOException or ArgumentException)
        {
            // Brak pliku — obiekt rysowany bez ikony
            return null;
        }
    }

    /// <summary>Źródło ikony typu obiektu.</summary>
    public string? ObjectIcon(int type) => _sources.GetValueOrDefault($"{TrackingObjectIcons.IconKey(type)}.png");

    /// <summary>
    /// Wskaźnik kierunku śladu (wektorowy, skierowany na północ; obracany o azymut): biały szewron z obrysem w kolorze akcentu.
    /// </summary>
    public string Arrow => _arrow ??= Svg(
        $"<path d='M6 16.5 L12 9.5 L18 16.5' fill='none' stroke='{Hex("FlotaAccentColor")}' stroke-width='6' stroke-linecap='round' stroke-linejoin='round'/>" +
        $"<path d='M6 16.5 L12 9.5 L18 16.5' fill='none' stroke='{Hex("SurfaceColor")}' stroke-width='3' stroke-linecap='round' stroke-linejoin='round'/>");

    /// <summary>Punkt postoju na śladzie: biała kropka z obwódką.</summary>
    public string Dot => _dot ??= Svg(
        $"<circle cx='12' cy='12' r='4.5' fill='{Hex("SurfaceColor")}' stroke='{Hex("FlotaAccentColor")}' stroke-width='2.5'/>");

    string? _arrow;
    string? _dot;

    static string Svg(string body) =>
        "svg-content://<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24'>" + body + "</svg>";

    /// <summary>Kolor z palety (Styles/ColorPaletteOverride.xaml) w postaci <c>#RRGGBB</c>.</summary>
    static string Hex(string key)
    {
        var c = Application.Current.Resources.TryGetValue(key, out var v) && v is Windows.UI.Color wc ? wc : Microsoft.UI.Colors.Gray;
        return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}
