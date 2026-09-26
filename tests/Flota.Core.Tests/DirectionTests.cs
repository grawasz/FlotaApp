using Flota.Core.Geo;

namespace Flota.Core.Tests;

public class DirectionTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 270)]
    [InlineData(-90, 90)]
    [InlineData(45, 315)]
    [InlineData(-135, 135)]
    [InlineData(180, 180)]
    public void FromUniAvlHeading_IsCounterClockwise(double heading, double azimuth) =>
        Assert.Equal(azimuth, Direction.FromUniAvlHeading(heading), 6);

    [Fact]
    public void Bearing_North() => Assert.Equal(0, Direction.Bearing(52, 21, 52.01, 21), 1);

    [Fact]
    public void Bearing_East() => Assert.Equal(90, Direction.Bearing(52, 21, 52, 21.01), 0);

    [Fact]
    public void Bearing_SouthWest() => Assert.InRange(Direction.Bearing(52, 21, 51.99, 20.985), 220, 230);

    [Fact]
    public void ArrowAzimuth_PrefersMovementDirection() =>
        Assert.Equal(90, Direction.ArrowAzimuth(52, 21, 0, 52, 21.01)!.Value, 0);

    [Fact]
    public void ArrowAzimuth_SamePoint_UsesHeading() =>
        Assert.Equal(270, Direction.ArrowAzimuth(52, 21, 90, 52, 21)!.Value, 6);

    [Fact]
    public void ArrowAzimuth_LastPoint_UsesHeading() =>
        Assert.Equal(135, Direction.ArrowAzimuth(52, 21, -135, null, null)!.Value, 6);

    [Fact]
    public void ArrowAzimuth_NoData_IsNull() => Assert.Null(Direction.ArrowAzimuth(52, 21, null, null, null));
}
