using System.Globalization;
using Flota.Client;
using Flota.Core.Contracts;

namespace Flota.Presentation.Dictionaries;

/// <summary>Kierowcy — słownik wczytywany przy pierwszym otwarciu panelu (plan, 6.1).</summary>
public sealed class DriversViewModel(FlotaApiClient api, Notifier notifier) : BusyViewModel(notifier)
{
    bool _loaded;

    public IReadOnlyList<DriverInfo> Drivers { get; private set; } = [];

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _loaded = true;
        _loaded = await RunAsync(async () =>
        {
            Drivers = await api.GetDriversAsync(CancellationToken.None);
            OnPropertyChanged(nameof(Drivers));
        });
    }
}

/// <summary>Wyposażenie obiektów z odświeżaniem pamięci podręcznej Flota.Api.</summary>
public sealed class EquipmentViewModel(FlotaApiClient api, Notifier notifier, TimeProvider time) : BusyViewModel(notifier)
{
    bool _loaded;

    public IReadOnlyList<EquipmentInfo> Items { get; private set; } = [];

    public Task EnsureLoadedAsync() => _loaded ? Task.CompletedTask : LoadAsync(refresh: false);

    public Task RefreshAsync() => LoadAsync(refresh: true);

    async Task LoadAsync(bool refresh)
    {
        _loaded = true;
        _loaded = await RunAsync(async () =>
        {
            Items = await api.GetEquipmentAsync(refresh, CancellationToken.None);
            OnPropertyChanged(nameof(Items));
        });
    }

    public string FileName => $"{Loc.Get("FileEquipment")} {time.GetLocalNow().DateTime.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture)}.xlsx";
}

/// <summary>Szablony zdarzeń i terminy (przypomnienia) wybranych obiektów.</summary>
public sealed class EventsViewModel(FlotaApiClient api, Notifier notifier) : BusyViewModel(notifier)
{
    public IReadOnlyList<EventTemplateInfo> Templates { get; private set; } = [];
    public IReadOnlyList<EventTemplateInfo> Reminders { get; private set; } = [];

    public async Task LoadAsync(IReadOnlyList<int> objectIds)
    {
        if (!RequireObjects(objectIds)) return;
        await RunAsync(async () =>
        {
            var all = await api.GetEventTemplatesAsync(objectIds, CancellationToken.None);
            Templates = all.Where(t => !t.IsReminder).ToList();
            Reminders = all.Where(t => t.IsReminder).ToList();
            OnPropertyChanged(nameof(Templates));
            OnPropertyChanged(nameof(Reminders));
        });
    }
}
