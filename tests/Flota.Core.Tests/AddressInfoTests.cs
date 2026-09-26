using Flota.Core.Geo;
using Flota.Core.Objects;

namespace Flota.Core.Tests;

public class AddressInfoTests
{
    [Fact]
    public void FullAddress_Poland_OmitsDistrict()
    {
        var a = new AddressInfo { Country = "PL", City = "Piaseczno", Province = "mazowieckie", District = "piaseczyński", Street = "Puławska", House = "12" };
        Assert.Equal("Piaseczno (mazowieckie),\nPuławska 12", a.FullAddress);
    }

    [Fact]
    public void FullAddress_Abroad_IncludesDistrict()
    {
        var a = new AddressInfo { Country = "DE", City = "Berlin", Province = "Berlin", District = "Mitte", Street = "Unter den Linden" };
        Assert.Equal("Berlin (Berlin - Mitte),\nUnter den Linden", a.FullAddress);
    }

    [Fact]
    public void FullAddress_HouseInStreet_NotDuplicated()
    {
        var a = new AddressInfo { Country = "PL", City = "Radom", Province = "mazowieckie", Street = "Żeromskiego 5", House = "5" };
        Assert.Equal("Radom (mazowieckie),\nŻeromskiego 5", a.FullAddress);
    }

    [Fact]
    public void Empty_FullAddressIsEmpty() => Assert.Equal(string.Empty, new AddressInfo().FullAddress);

    [Fact]
    public void CityOrProvince_FallsBackToProvince() =>
        Assert.Equal("mazowieckie", new AddressInfo { Province = "mazowieckie" }.CityOrProvince);

    [Fact]
    public void StreetOrDistrict_FallsBackToDistrict() =>
        Assert.Equal("grójecki", new AddressInfo { District = "grójecki" }.StreetOrDistrict);

    [Fact]
    public void SingleLine_JoinsWithComma()
    {
        var a = new AddressInfo { Country = "PL", City = "Otwock", Province = "mazowieckie", Street = "Andriollego" };
        Assert.Equal("Otwock (mazowieckie), Andriollego", a.SingleLine);
    }

    [Theory]
    [InlineData(0, "to_car")]
    [InlineData(2, "to_truck")]
    [InlineData(22, "to_excavator")]
    [InlineData(23, "to_car")]
    [InlineData(-1, "to_car")]
    public void IconKey_MapsType(int type, string expected) => Assert.Equal(expected, TrackingObjectIcons.IconKey(type));
}
