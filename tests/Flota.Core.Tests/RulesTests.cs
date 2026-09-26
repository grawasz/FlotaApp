using Flota.Core.Geo;
using Flota.Core.Objects;
using Flota.Core.Reports;
using Flota.Core.Series;

namespace Flota.Core.Tests;

public class GeoConversionsTests
{
    [Fact]
    public void Win32ToHex_SwapsByteOrder() => Assert.Equal("#FF8000", GeoConversions.Win32ToHex(0x0080FF));

    [Fact]
    public void ParseWktPolygon_ReadsLonLat()
    {
        var p = GeoConversions.ParseWktPolygon("POLYGON ((21.01 52.23, 21.02 52.24, 21.03 52.23, 21.01 52.23))");
        Assert.Equal(4, p.Count);
        Assert.Equal(52.23, p[0].Latitude);
        Assert.Equal(21.01, p[0].Longitude);
    }

    [Fact]
    public void ParseWktPolygon_SkipsInvalidPoints() =>
        Assert.Single(GeoConversions.ParseWktPolygon("POLYGON ((21.01 52.23, abc def, 1))"));

    [Fact]
    public void ParseWktPolygon_Empty() => Assert.Empty(GeoConversions.ParseWktPolygon(null));

    [Fact]
    public void AddressKey_RoundsToFourDecimals()
    {
        Assert.Equal(GeoConversions.AddressKey(52.230049, 21.010049), GeoConversions.AddressKey(52.23001, 21.01001));
        Assert.NotEqual(GeoConversions.AddressKey(52.2301, 21.0100), GeoConversions.AddressKey(52.2300, 21.0100));
    }
}

public class DeviceClassifierTests
{
    [Theory]
    [InlineData("Lincor LX Micro", "GPS CAN")]
    [InlineData("Skaut 2", "GPS CAN")]
    [InlineData("Hytera", "GPS standard")]
    [InlineData(null, "GPS standard")]
    public void Classify(string? model, string expected) => Assert.Equal(expected, DeviceClassifier.Classify(model));

    [Fact]
    public void NoDevice_IsEmpty() => Assert.Equal(string.Empty, DeviceClassifier.Classify("Skaut 2", hasDevice: false));
}

public class ReportRulesTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(1.0, 1.0)]
    [InlineData(1.5, 0.0)]
    [InlineData(0.25, 75.0)]
    [InlineData(0.0, 100.0)]
    public void DrivingFeatureScore(double? raw, double? expected) => Assert.Equal(expected, ReportRules.DrivingFeatureScore(raw));

    [Fact]
    public void ConsumptionPer100Km() => Assert.Equal(8.0, ReportRules.ConsumptionPer100Km(16, 200));

    [Fact]
    public void ConsumptionPer100Km_ZeroDistance_IsNull() => Assert.Null(ReportRules.ConsumptionPer100Km(16, 0));

    [Fact]
    public void ConsumptionPerHour() => Assert.Equal(4.0, ReportRules.ConsumptionPerHour(8, TimeSpan.FromHours(2)));

    [Fact]
    public void FuelLevel_UsesTankCapacity() => Assert.Equal(60.0, ReportRules.FuelLevel(0.5, 120));

    [Fact]
    public void FormatDuration_KeepsHoursOver24() => Assert.Equal("26:03:04", ReportRules.FormatDuration(new TimeSpan(1, 2, 3, 4)));

    [Fact]
    public void DrivingFeatureName() => Assert.Equal("Gwałtowne hamowanie", ReportRules.DrivingFeatureName(2));
}

public class LttbTests
{
    [Fact]
    public void ShortSeries_Unchanged()
    {
        var data = Enumerable.Range(0, 10).ToList();
        Assert.Same(data, Lttb.Downsample(data, 100, x => x, y => y));
    }

    [Fact]
    public void Downsample_KeepsEndpointsAndCount()
    {
        var data = Enumerable.Range(0, 10_000).Select(i => (X: (double)i, Y: Math.Sin(i / 100.0))).ToList();
        var r = Lttb.Downsample(data, 500, p => p.X, p => p.Y);
        Assert.Equal(500, r.Count);
        Assert.Equal(data[0], r[0]);
        Assert.Equal(data[^1], r[^1]);
    }

    [Fact]
    public void Downsample_KeepsPeak()
    {
        var data = Enumerable.Range(0, 1000).Select(i => (X: (double)i, Y: i == 500 ? 100.0 : 0.0)).ToList();
        var r = Lttb.Downsample(data, 50, p => p.X, p => p.Y);
        Assert.Contains(r, p => p.Y == 100.0);
    }
}
