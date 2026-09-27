using Flota.Core.Status;
using Flota.Presentation.Fleet;
using Microsoft.Extensions.Time.Testing;

namespace Flota.Presentation.Tests;

/// <summary>Obiekt listy i mapy: scalanie pozycji (odpowiednik <c>TrackingMessage.Load</c>), adres, widoczność, status.</summary>
public sealed class ObjectItemTests
{
    readonly FakeTimeProvider _clock = new(new DateTimeOffset(Data.Now, TimeSpan.Zero)) { };

    public ObjectItemTests() => _clock.SetLocalTimeZone(TimeZoneInfo.Utc);

    ObjectItem Item(Core.Contracts.ObjectPosition? position) => new(Data.Object(1, "Ciężarówka", position), _clock);

    [Fact]
    public void ApplyPosition_OlderMessage_Ignored()
    {
        var item = Item(Data.Position(Data.Now));
        item.ApplyPosition(Data.Position(Data.Now.AddMinutes(-1), lat: 50));
        Assert.Equal(52.23, item.Position!.Latitude);
    }

    [Fact]
    public void ApplyPosition_InvalidGps_KeepsLocationAndAddress()
    {
        var item = Item(Data.Position(Data.Now.AddMinutes(-1), address: Data.Address("Warszawa")));
        item.ApplyPosition(Data.Position(Data.Now, lat: 0, lon: 0, validGps: false, engine: false));

        Assert.Equal(52.23, item.Position!.Latitude);
        Assert.Equal("Warszawa", item.Position.Address!.City);
        Assert.Equal(Data.Now, item.Position.Time);
        Assert.False(item.Position.IsEngineStarted);
    }

    [Fact]
    public void ApplyPosition_SamePlace_KeepsAddress_NewPlace_ClearsAddress()
    {
        var item = Item(Data.Position(Data.Now.AddMinutes(-2), address: Data.Address("Warszawa")));
        item.ApplyPosition(Data.Position(Data.Now.AddMinutes(-1), lat: 52.23001));
        Assert.Equal("Warszawa", item.Position!.Address?.City);

        item.ApplyPosition(Data.Position(Data.Now, lat: 52.5));
        Assert.Null(item.Position!.Address);
    }

    [Fact]
    public void ApplyPosition_FirstPosition_ShowsObjectOnMap()
    {
        var item = Item(null);
        Assert.False(item.Enabled);
        Assert.False(item.IsVisibleOnMap);

        item.ApplyPosition(Data.Position(Data.Now));
        Assert.True(item.Enabled);
        Assert.True(item.IsVisibleOnMap);
    }

    [Fact]
    public void IsVisibleOnMap_WithoutPosition_AlwaysFalse()
    {
        var item = Item(null);
        item.IsVisibleOnMap = true;
        Assert.False(item.IsVisibleOnMap);
    }

    [Fact]
    public void ApplyAddress_OnlyForCurrentMessage()
    {
        var item = Item(Data.Position(Data.Now));
        item.ApplyAddress(Data.Now.AddMinutes(-1), Data.Address("Kraków"));
        Assert.Null(item.Position!.Address);

        item.ApplyAddress(Data.Now, Data.Address("Kraków"));
        Assert.Equal("Kraków", item.LocationText);
    }

    [Fact]
    public void Replace_WithoutPosition_HidesObject()
    {
        var item = Item(Data.Position(Data.Now));
        Assert.True(item.IsVisibleOnMap);
        item.Replace(Data.Object(1, "Ciężarówka"));
        Assert.False(item.IsVisibleOnMap);
    }

    [Fact]
    public void Status_DependsOnClock()
    {
        var item = Item(Data.Position(Data.Now));
        Assert.Equal(TrackingObjectStatus.Move, item.Status);
        Assert.Equal("W ruchu", item.StatusName);

        _clock.Advance(TimeSpan.FromMinutes(61));
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        item.RefreshStatus();

        Assert.Equal(TrackingObjectStatus.Outdated, item.Status);
        Assert.Contains(nameof(ObjectItem.Status), raised);
        Assert.Contains(nameof(ObjectItem.StatusName), raised);
    }

    [Fact]
    public void Tooltip_And_IconUri()
    {
        var item = new ObjectItem(Data.Object(1, "Autobus", Data.Position(Data.Now), "WX 1") with { Type = 4 }, _clock);
        Assert.Equal("Autobus (WX 1) — W ruchu", item.Tooltip);
        Assert.Equal("ms-appx:///Assets/Images/to_bus.png", item.IconUri);
    }
}
