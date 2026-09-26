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
            .Select(k => $"{k}.png")
            .Concat(["arrows-o/arrow_0.png", "arrows-o/dot.png"]);
        foreach (var name in names)
        {
            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/Images/{name}"));
                var buffer = await FileIO.ReadBufferAsync(file);
                var bytes = new byte[buffer.Length];
                using (var reader = Windows.Storage.Streams.DataReader.FromBuffer(buffer)) reader.ReadBytes(bytes);
                _sources[name] = "base64-content://" + Convert.ToBase64String(bytes);
            }
            catch (Exception ex) when (ex is FileNotFoundException or IOException or ArgumentException)
            {
                // Brak pliku — obiekt rysowany bez ikony
            }
        }
        IsLoaded = true;
    }

    /// <summary>Źródło ikony typu obiektu.</summary>
    public string? ObjectIcon(int type) => _sources.GetValueOrDefault($"{TrackingObjectIcons.IconKey(type)}.png");

    public string? Arrow => _sources.GetValueOrDefault("arrows-o/arrow_0.png");

    public string? Dot => _sources.GetValueOrDefault("arrows-o/dot.png");
}
