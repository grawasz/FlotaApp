using Flota.Core.Contracts;

namespace Flota.Presentation;

/// <summary>Ostatni znany stan floty na urządzeniu — natychmiastowe wyświetlenie przy starcie (plan, 6.1, P4).</summary>
public interface ISnapshotStore
{
    Task<FleetSnapshot?> LoadAsync();

    Task SaveAsync(FleetSnapshot snapshot);

    Task ClearAsync();
}
