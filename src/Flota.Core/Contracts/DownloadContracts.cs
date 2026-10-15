namespace Flota.Core.Contracts;

/// <summary>
/// Plik instalacyjny aplikacji udostępniany przez Flota.Api (<c>GET /downloads/android</c>): nazwa, adres pobrania względem
/// adresu usługi, rozmiar, data modyfikacji pliku i wersja (z konfiguracji albo z manifestu pakietu; <c>null</c>, gdy nieznana).
/// </summary>
public sealed record AppDownloadInfo(string FileName, string Url, long Size, DateTimeOffset Modified, string? Version);
