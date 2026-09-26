using System.Text.Json;
using Flota.Core.Contracts;
using Windows.Storage;

namespace Flota.App.Services;

/// <summary>Ostatni znany stan floty na urządzeniu — natychmiastowe wyświetlenie przy starcie (plan, 6.1, P4).</summary>
public sealed class SnapshotCache
{
    const string FileName = "fleet-snapshot.json";

    public async Task<FleetSnapshot?> LoadAsync()
    {
        try
        {
            var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileName);
            if (item is not StorageFile file) return null;
            await using var stream = await file.OpenStreamForReadAsync();
            return await JsonSerializer.DeserializeAsync(stream, FlotaJsonContext.Default.FleetSnapshot);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task SaveAsync(FleetSnapshot snapshot)
    {
        try
        {
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(FileName, CreationCollisionOption.ReplaceExisting);
            await using var stream = await file.OpenStreamForWriteAsync();
            await JsonSerializer.SerializeAsync(stream, snapshot, FlotaJsonContext.Default.FleetSnapshot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Pamięć lokalna niedostępna — pominięcie zapisu
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            if (await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileName) is StorageFile file)
                await file.DeleteAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
