using CommunityToolkit.Mvvm.ComponentModel;
using Flota.Core.Contracts;
using Flota.Core.Geo;
using Flota.Core.Objects;
using Flota.Core.Status;

namespace Flota.Presentation.Fleet;

/// <summary>Obiekt floty na liście i mapie.</summary>
public sealed partial class ObjectItem : ObservableObject
{
    readonly TimeProvider _clock;
    FleetObject _source;

    /// <param name="clock">Zegar statusu (próg nieaktualności).</param>
    public ObjectItem(FleetObject source, TimeProvider clock)
    {
        _source = source;
        _clock = clock;
        _isVisibleOnMap = source.Enabled;
    }

    public FleetObject Source => _source;
    public int Id => _source.Id;
    public string Name => _source.Name;
    public string RegistrationNumber => _source.RegistrationNumber;
    public int Type => _source.Type;
    public bool IsActive => _source.IsActive;
    public int? TankCapacity => _source.TankCapacity;
    public ObjectPosition? Position => _source.Position;

    /// <summary>Obiekt ma pozycję — może być pokazany na mapie.</summary>
    public bool Enabled => _source.Enabled;

    public string IconUri => $"ms-appx:///Assets/Images/{TrackingObjectIcons.IconKey(_source.Type)}.png";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    bool _isVisibleOnMap;

    /// <summary>Widoczność na mapie (kolumna „✓”).</summary>
    public bool IsVisibleOnMap
    {
        get => _isVisibleOnMap;
        set
        {
            if (!Enabled) value = false;
            SetProperty(ref _isVisibleOnMap, value);
        }
    }

    public TrackingObjectStatus Status => _source.StatusAt(_clock.GetLocalNow().DateTime);

    public string StatusName => TrackingObjectStatusResolver.DisplayName(Status);

    public string LocationText => Position?.Address is { IsEmpty: false } a && Enabled ? a.CityOrProvince : string.Empty;

    public string Tooltip => string.IsNullOrEmpty(RegistrationNumber) ? $"{Name} — {StatusName}" : $"{Name} ({RegistrationNumber}) — {StatusName}";

    /// <summary>
    /// Scalenie nowej pozycji (odpowiednik <c>TrackingMessage.Load</c>): położenie zastępowane tylko przy poprawnym GPS.
    /// </summary>
    public void ApplyPosition(ObjectPosition next)
    {
        var current = _source.Position;
        if (current is not null && next.Time < current.Time) return;
        var merged = current is not null && !(next.ValidGps && next.HasLocation)
            ? next with { Latitude = current.Latitude, Longitude = current.Longitude, Address = current.Address }
            : next with { Address = next.Address ?? (current is not null && SamePlace(current, next) ? current.Address : null) };
        var wasEnabled = Enabled;
        _source = _source with { Position = merged };
        if (!wasEnabled && Enabled) IsVisibleOnMap = true;
        RaiseAll();
    }

    public void ApplyAddress(DateTime time, AddressInfo address)
    {
        if (_source.Position is null || _source.Position.Time != time) return;
        _source = _source with { Position = _source.Position with { Address = address } };
        RaiseAll();
    }

    public void Replace(FleetObject source)
    {
        _source = source;
        if (!Enabled) IsVisibleOnMap = false;
        RaiseAll();
    }

    /// <summary>Przeliczenie statusu (upływ czasu — próg nieaktualności).</summary>
    public void RefreshStatus()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusName));
    }

    void RaiseAll() => OnPropertyChanged(string.Empty);

    static bool SamePlace(ObjectPosition a, ObjectPosition b) =>
        GeoConversions.AddressKey(a.Latitude, a.Longitude) == GeoConversions.AddressKey(b.Latitude, b.Longitude);
}
