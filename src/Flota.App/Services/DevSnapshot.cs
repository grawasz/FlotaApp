#if DEBUG
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Flota.App.Services;

/// <summary>
/// Narzędzie deweloperskie (tylko DEBUG): zrzut okna aplikacji na żądanie — plik <c>snap-request</c>
/// w katalogu danych lokalnych powoduje zapis <c>snap.png</c> w tym samym katalogu.
/// </summary>
internal static class DevSnapshot
{
    public static void Start(Window window)
    {
        var folder = ApplicationData.Current.LocalFolder.Path;
        var request = Path.Combine(folder, "snap-request");
        var dispatcher = window.DispatcherQueue;
        _ = Task.Run(async () =>
        {
            var commands = Path.Combine(folder, "dev-command");
            while (true)
            {
                await Task.Delay(1000);
                if (File.Exists(commands))
                {
                    var lines = await File.ReadAllLinesAsync(commands);
                    File.Delete(commands);
                    var ran = new TaskCompletionSource();
                    dispatcher.TryEnqueue(async () =>
                    {
                        try { await RunCommandsAsync(lines); }
                        catch (Exception ex) { File.AppendAllText(Path.Combine(Path.GetTempPath(), "flota-app-error.log"), "DEVCMD " + ex + Environment.NewLine); }
                        finally { ran.TrySetResult(); }
                    });
                    await ran.Task;
                }
                if (!File.Exists(request)) continue;
                try { File.Delete(request); } catch (IOException) { continue; }
                var done = new TaskCompletionSource();
                dispatcher.TryEnqueue(async () =>
                {
                    try
                    {
                        await CaptureAsync(window, Path.Combine(folder, "snap.png"));
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(Path.Combine(Path.GetTempPath(), "flota-app-error.log"), "SNAPSHOT " + ex + Environment.NewLine);
                    }
                    finally
                    {
                        done.TrySetResult();
                    }
                });
                await done.Task;
            }
        });
    }

    /// <summary>Polecenia: <c>select &lt;id&gt;</c>, <c>track</c>, <c>zoom &lt;poziom&gt;</c>, <c>wait &lt;ms&gt;</c>.</summary>
    static async Task RunCommandsAsync(IEnumerable<string> lines)
    {
        var fleet = App.Services.GetRequiredService<Presentation.Fleet.FleetViewModel>();
        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            switch (parts[0])
            {
                case "select" when parts.Length > 1 && fleet.Find(int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)) is { } o:
                    fleet.Select(o);
                    break;
                case "track":
                    await fleet.ToggleTrackingAsync();
                    break;
                case "zoom" when parts.Length > 1 && fleet.SelectedObject?.Position is { } p:
                    fleet.ZoomTo(fleet.SelectedObject);
                    break;
                case "wait" when parts.Length > 1:
                    await Task.Delay(int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
                    break;
            }
        }
    }

    static async Task CaptureAsync(Window window, string path)
    {
        if (window.Content is not UIElement root) return;
        var rtb = new RenderTargetBitmap();
        await rtb.RenderAsync(root);
        var pixels = (await rtb.GetPixelsAsync()).ToArray();
        using var ms = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ms);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)rtb.PixelWidth, (uint)rtb.PixelHeight, 96, 96, pixels);
        await encoder.FlushAsync();
        ms.Seek(0);
        var bytes = new byte[ms.Size];
        await ms.ReadAsync(bytes.AsBuffer(), (uint)ms.Size, Windows.Storage.Streams.InputStreamOptions.None);
        await File.WriteAllBytesAsync(path, bytes);
    }
}
#endif
