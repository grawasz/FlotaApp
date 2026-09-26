namespace Flota.App.Models;

/// <summary>Konfiguracja aplikacji (appsettings.json).</summary>
public record AppConfig
{
    public string? Environment { get; init; }

    /// <summary>Adres Flota.Api, np. <c>https://serwer/</c>.</summary>
    public string? ApiBaseUrl { get; init; }

    /// <summary>Adres Flota.Api dla emulatora Android (host maszyny deweloperskiej).</summary>
    public string? ApiBaseUrlAndroidEmulator { get; init; }

    /// <summary>Szablon adresu kafelków mapy.</summary>
    public string TileUrl { get; init; } = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
}
