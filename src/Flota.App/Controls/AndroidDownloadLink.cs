using Flota.App.Services;
using Flota.Core.Contracts;

namespace Flota.App.Controls;

/// <summary>
/// Odnośnik „Pobierz aplikację na Android” z wersją, rozmiarem i datą pliku (T9.5). Wyświetlany wyłącznie w wersji
/// przeglądarkowej i tylko wtedy, gdy Flota.Api udostępnia pakiet (<c>GET /downloads/android</c>).
/// </summary>
public sealed partial class AndroidDownloadLink : UserControl
{
    readonly HyperlinkButton _link = new() { HorizontalAlignment = HorizontalAlignment.Center };
    readonly TextBlock _details = new() { HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };

    public AndroidDownloadLink()
    {
        Visibility = Visibility.Collapsed;
        _link.Content = Loc.Get("DownloadAndroid");
        AutomationProperties.SetName(_link, Loc.Get("DownloadAndroid"));
        if (Application.Current.Resources.TryGetValue("FlotaCaptionTextStyle", out var style) && style is Style s) _details.Style = s;
        Content = new StackPanel { Spacing = 2, Children = { _link, _details } };
        Loaded += async (_, _) => await ShowAsync();
    }

    async Task ShowAsync()
    {
        var download = App.Services.GetRequiredService<AndroidDownload>();
        if (await download.GetAsync() is not { } info || download.Link(info) is not { } uri) return;
        _link.NavigateUri = uri;
        _details.Text = AndroidDownload.Details(info);
        Visibility = Visibility.Visible;
    }
}
