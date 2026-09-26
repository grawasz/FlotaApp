using Flota.Core.Status;

namespace Flota.Core.Tests;

public class TrackingObjectStatusResolverTests
{
    static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0);

    [Fact]
    public void Disabled_IsUnknown() =>
        Assert.Equal(TrackingObjectStatus.Unknown, TrackingObjectStatusResolver.Resolve(false, Now, true, true, Now));

    [Fact]
    public void NoMessage_IsUnknown() =>
        Assert.Equal(TrackingObjectStatus.Unknown, TrackingObjectStatusResolver.Resolve(true, null, true, true, Now));

    [Fact]
    public void MinValueTime_IsUnknown() =>
        Assert.Equal(TrackingObjectStatus.Unknown, TrackingObjectStatusResolver.Resolve(true, DateTime.MinValue, true, true, Now));

    [Fact]
    public void OlderThanHour_IsOutdated() =>
        Assert.Equal(TrackingObjectStatus.Outdated, TrackingObjectStatusResolver.Resolve(true, Now.AddMinutes(-61), true, true, Now));

    [Fact]
    public void ExactlyHour_IsNotOutdated() =>
        Assert.Equal(TrackingObjectStatus.Move, TrackingObjectStatusResolver.Resolve(true, Now.AddMinutes(-60), true, true, Now));

    [Fact]
    public void OutdatedTakesPrecedenceOverInvalidGps() =>
        Assert.Equal(TrackingObjectStatus.Outdated, TrackingObjectStatusResolver.Resolve(true, Now.AddHours(-2), false, true, Now));

    [Fact]
    public void InvalidGps_IsInvalidGps() =>
        Assert.Equal(TrackingObjectStatus.InvalidGps, TrackingObjectStatusResolver.Resolve(true, Now, false, true, Now));

    [Fact]
    public void EngineOff_IsStop() =>
        Assert.Equal(TrackingObjectStatus.Stop, TrackingObjectStatusResolver.Resolve(true, Now, true, false, Now));

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public void EngineOnOrUnknown_IsMove(bool? engine) =>
        Assert.Equal(TrackingObjectStatus.Move, TrackingObjectStatusResolver.Resolve(true, Now, true, engine, Now));
}
