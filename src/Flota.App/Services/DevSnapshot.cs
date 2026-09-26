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
        var timer = window.DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += async (_, _) =>
        {
            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                if (await folder.TryGetItemAsync("snap-request") is not StorageFile request) return;
                await request.DeleteAsync();
                if (window.Content is not UIElement root) return;
                var rtb = new RenderTargetBitmap();
                await rtb.RenderAsync(root);
                var pixels = (await rtb.GetPixelsAsync()).ToArray();
                var file = await folder.CreateFileAsync("snap.png", CreationCollisionOption.ReplaceExisting);
                using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)rtb.PixelWidth, (uint)rtb.PixelHeight, 96, 96, pixels);
                await encoder.FlushAsync();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
            {
            }
        };
        timer.Start();
    }
}
#endif
