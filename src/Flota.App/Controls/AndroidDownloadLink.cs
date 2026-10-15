using Flota.App.Services;
using Flota.Core.Contracts;

namespace Flota.App.Controls;

/// <summary>
/// Odnośnik „Pobierz aplikację na Android” z wersją, rozmiarem i datą pliku (T9.5). Wyświetlany wyłącznie w wersji
/// przeglądarkowej i tylko wtedy, gdy Flota.Api udostępnia pakiet (<c>GET /downloads/android</c>). Elementy tworzone
/// dopiero po potwierdzeniu pakietu — bez pakietu kontrolka pusta (również w warstwie dostępności).
/// </summary>
public sealed partial class AndroidDownloadLink : UserControl
{
    public AndroidDownloadLink()
    {
        Visibility = Visibility.Collapsed;
        Loaded += async (_, _) => await ShowAsync();
    }

    async Task ShowAsync()
    {
        if (Content is not null) return;
        var download = App.Services.GetRequiredService<AndroidDownload>();
        if (await download.GetAsync() is not { } info || download.Link(info) is not { } uri) return;

        var link = new HyperlinkButton { HorizontalAlignment = HorizontalAlignment.Center, NavigateUri = uri, Content = Loc.Get("DownloadAndroid") };
        AutomationProperties.SetName(link, Loc.Get("DownloadAndroid"));
        var details = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Text = AndroidDownload.Details(info),
        };
        if (Application.Current.Resources.TryGetValue("FlotaCaptionTextStyle", out var style) && style is Style s) details.Style = s;
        Content = new StackPanel { Spacing = 2, Children = { link, details } };
        Visibility = Visibility.Visible;
    }
}
