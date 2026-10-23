using SchichtWerk.Domain;
using static SchichtWerk.Tests.TestScenarios;

namespace SchichtWerk.Tests;

/// <summary>Invalid intervals and shifts must be rejected instead of producing misleading numbers.</summary>
public class ValidationTests
{
    [Fact]
    public void Interval_RejectsNegativeCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Running(0, good: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Running(0, good: 9, rejected: -1));
    }

    [Fact]
    public void Interval_RejectsNonPositiveDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProductionInterval(0, TimeSpan.Zero, TimeSpan.Zero, IntervalState.Running));
    }

    [Fact]
    public void Interval_RequiresReasonExactlyForDowntime()
    {
        Assert.Throws<ArgumentException>(() =>
            new ProductionInterval(0, TimeSpan.Zero, Five, IntervalState.Downtime, reason: null));
        Assert.Throws<ArgumentException>(() =>
            new ProductionInterval(0, TimeSpan.Zero, Five, IntervalState.Running, DowntimeReason.Changeover));
    }

    [Theory]
    [InlineData(IntervalState.Downtime)]
    [InlineData(IntervalState.ScheduledBreak)]
    public void Interval_StoppedMachineCannotProduceParts(IntervalState state)
    {
        DowntimeReason? reason = state == IntervalState.Downtime ? DowntimeReason.MachineFault : null;

        Assert.Throws<ArgumentException>(() =>
            new ProductionInterval(0, TimeSpan.Zero, Five, state, reason, goodCount: 1));
    }

    [Fact]
    public void Shift_RejectsGapBetweenIntervals()
    {
        var gap = new ProductionInterval(1, TimeSpan.FromMinutes(10), Five, IntervalState.Running, goodCount: 9);

        var ex = Assert.Throws<ArgumentException>(() =>
            new ShiftScenario(DemoScenarioFactory.Info, TimeSpan.FromMinutes(15), Cycle, [Running(0, 9), gap]));
        Assert.Contains("Gap", ex.Message);
    }

    [Fact]
    public void Shift_RejectsOverlappingIntervals()
    {
        var overlap = new ProductionInterval(1, TimeSpan.FromMinutes(3), Five, IntervalState.Running, goodCount: 9);

        var ex = Assert.Throws<ArgumentException>(() =>
            new ShiftScenario(DemoScenarioFactory.Info, TimeSpan.FromMinutes(8), Cycle, [Running(0, 9), overlap]));
        Assert.Contains("overlaps", ex.Message);
    }

    [Fact]
    public void Shift_RejectsUnorderedIntervals()
    {
        Assert.Throws<ArgumentException>(() => Shift(Running(1, 9), Running(0, 9)));
    }

    [Fact]
    public void Shift_RejectsIntervalsThatDoNotCoverTheShift()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new ShiftScenario(DemoScenarioFactory.Info, TimeSpan.FromMinutes(480), Cycle, [Running(0, 9)]));
        Assert.Contains("cover", ex.Message);
    }

    [Fact]
    public void Shift_AllowsExactlyIdealSpeed()
    {
        // 10 pieces × 30 s = 5 min: performance exactly 100 %
        var shift = Shift(Running(0, good: 10));

        Assert.Equal(1.0, OeeCalculator.Calculate(shift).Performance);
    }

    [Fact]
    public void Shift_RejectsPerformanceAbove100Percent()
    {
        // 11 pieces × 30 s = 5.5 min, but the interval only lasts 5 min
        var ex = Assert.Throws<ArgumentException>(() => Shift(Running(0, good: 10, rejected: 1)));
        Assert.Contains("ideal cycle time", ex.Message);
    }

    [Fact]
    public void Shift_RejectsNonPositiveCycleTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShiftScenario(DemoScenarioFactory.Info, Five, TimeSpan.Zero, [Running(0, 9)]));
    }

    [Fact]
    public void Shift_CannotBeChangedThroughTheOriginalList()
    {
        var source = new[] { Running(0, 9) };
        var shift = Shift(source);

        source[0] = Running(0, 1);

        Assert.Equal(9, shift.Intervals[0].GoodCount);
    }
}
