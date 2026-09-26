using Flota.Core.Periods;

namespace Flota.Core.Tests;

public class PeriodPresetTests
{
    // Czwartek
    static readonly DateTime Now = new(2026, 9, 24, 14, 35, 10);

    [Fact]
    public void LastHour_ReturnsHourBeforeNow()
    {
        var (from, to) = PeriodPreset.Compute(PeriodType.LastHour, Now);
        Assert.Equal(Now.AddHours(-1), from);
        Assert.Equal(Now, to);
    }

    [Fact]
    public void Today_ReturnsWholeDay()
    {
        var (from, to) = PeriodPreset.Compute(PeriodType.Today, Now);
        Assert.Equal(new DateTime(2026, 9, 24), from);
        Assert.Equal(new DateTime(2026, 9, 24, 23, 59, 59), to);
    }

    [Fact]
    public void Yesterday_ReturnsPreviousDay()
    {
        var (from, to) = PeriodPreset.Compute(PeriodType.Yesterday, Now);
        Assert.Equal(new DateTime(2026, 9, 23), from);
        Assert.Equal(new DateTime(2026, 9, 23, 23, 59, 59), to);
    }

    [Fact]
    public void CurrentWeek_StartsOnMonday()
    {
        var (from, to) = PeriodPreset.Compute(PeriodType.CurrentWeek, Now);
        Assert.Equal(new DateTime(2026, 9, 21), from);
        Assert.Equal(new DateTime(2026, 9, 27, 23, 59, 59), to);
    }

    [Fact]
    public void CurrentWeek_OnSunday_StartsOnPreviousMonday()
    {
        var (from, _) = PeriodPreset.Compute(PeriodType.CurrentWeek, new DateTime(2026, 9, 27, 10, 0, 0));
        Assert.Equal(new DateTime(2026, 9, 21), from);
    }

    [Fact]
    public void LastWeek_ReturnsPreviousMondayToSunday()
    {
        var (from, to) = PeriodPreset.Compute(PeriodType.LastWeek, Now);
        Assert.Equal(new DateTime(2026, 9, 14), from);
        Assert.Equal(new DateTime(2026, 9, 20, 23, 59, 59), to);
    }

    [Fact]
    public void CurrentMonth_ReturnsWholeMonth()
    {
        var (from, to) = PeriodPreset.Compute(PeriodType.CurrentMonth, Now);
        Assert.Equal(new DateTime(2026, 9, 1), from);
        Assert.Equal(new DateTime(2026, 9, 30, 23, 59, 59), to);
    }

    [Fact]
    public void LastMonth_InJanuary_ReturnsDecemberOfPreviousYear()
    {
        var (from, to) = PeriodPreset.Compute(PeriodType.LastMonth, new DateTime(2027, 1, 15));
        Assert.Equal(new DateTime(2026, 12, 1), from);
        Assert.Equal(new DateTime(2026, 12, 31, 23, 59, 59), to);
    }

    [Fact]
    public void NonStandard_KeepsCurrentRange()
    {
        var current = (new DateTime(2026, 1, 2), new DateTime(2026, 1, 3));
        Assert.Equal(current, PeriodPreset.Compute(PeriodType.NonStandard, Now, current));
    }

    [Theory]
    [InlineData(PeriodType.LastHour, "Ostatnia godzina")]
    [InlineData(PeriodType.NonStandard, "Niestandardowy")]
    public void DisplayName_ReturnsPolishLabel(PeriodType type, string expected) =>
        Assert.Equal(expected, PeriodPreset.DisplayName(type));
}
