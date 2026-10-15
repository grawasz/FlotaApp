using Flota.Client;
using Flota.Core.Contracts;

namespace Flota.App.Services;

/// <summary>
/// Pakiet Android udostępniany przez Flota.Api (T9.5) — metadane pobierane raz na uruchomienie, wyłącznie w wersji
/// przeglądarkowej (aplikacje natywne nie wyświetlają odnośnika).
/// </summary>
public sealed class AndroidDownload(FlotaApiClient api)
{
    Task<AppDownloadInfo?>? _info;

    public Task<AppDownloadInfo?> GetAsync() => _info ??= LoadAsync();

    async Task<AppDownloadInfo?> LoadAsync()
    {
        if (!OperatingSystem.IsBrowser() || api.BaseAddress is null) return null;
        try
        {
            return await api.GetAndroidDownloadAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is HttpRequestException or FlotaApiException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>Adres pobrania względem adresu Flota.Api.</summary>
    public Uri? Link(AppDownloadInfo info) => api.BaseAddress is { } b ? new Uri(b, info.Url) : null;

    /// <summary>Opis pliku: wersja (gdy znana), rozmiar w MB, data pliku.</summary>
    public static string Details(AppDownloadInfo info)
    {
        var size = Loc.N1(info.Size / 1024d / 1024d);
        var date = Loc.Date(info.Modified.LocalDateTime);
        return info.Version is { } version
            ? Loc.Format("DownloadAndroidDetailsVersion", version, size, date)
            : Loc.Format("DownloadAndroidDetails", size, date);
    }
}
