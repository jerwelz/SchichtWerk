using SchichtWerk.Domain;
using static SchichtWerk.Tests.TestScenarios;

namespace SchichtWerk.Tests;

/// <summary>The building blocks the scenario actions use must refuse impossible changes.</summary>
public class IntervalChangeTests
{
    [Fact]
    public void ToDowntime_KeepsTimeSlotAndRemovesParts()
    {
        var changed = Running(3, good: 8, rejected: 1).ToDowntime(DowntimeReason.MaterialShortage);

        Assert.Equal(3, changed.Index);
        Assert.Equal(15 * TimeSpan.FromMinutes(1), changed.Start);
        Assert.Equal(IntervalState.Downtime, changed.State);
        Assert.Equal(DowntimeReason.MaterialShortage, changed.Reason);
        Assert.Equal(0, changed.TotalCount);
    }

    [Fact]
    public void ToDowntime_RejectsBreakAndExistingDowntime()
    {
        Assert.Throws<InvalidOperationException>(() => Break(0).ToDowntime(DowntimeReason.MaterialShortage));
        Assert.Throws<InvalidOperationException>(() => Down(0).ToDowntime(DowntimeReason.MaterialShortage));
    }

    [Fact]
    public void WithAdditionalRejects_KeepsTotal()
    {
        var changed = Running(0, good: 9).WithAdditionalRejects(5);

        Assert.Equal(4, changed.GoodCount);
        Assert.Equal(5, changed.RejectedCount);
        Assert.Equal(9, changed.TotalCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WithAdditionalRejects_RequiresPositiveCount(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Running(0, good: 9).WithAdditionalRejects(count));
    }

    [Fact]
    public void WithAdditionalRejects_CannotExceedGoodParts()
    {
        Assert.Throws<InvalidOperationException>(() => Running(0, good: 4, rejected: 5).WithAdditionalRejects(5));
    }

    [Fact]
    public void WithChangedIntervals_LeavesOriginalUnchangedAndValidatesResult()
    {
        var original = Shift(Running(0, 9), Running(1, 9));

        var changed = original.WithChangedIntervals([1], i => i.ToDowntime(DowntimeReason.Changeover));

        Assert.Equal(IntervalState.Running, original.Intervals[1].State);
        Assert.Equal(IntervalState.Downtime, changed.Intervals[1].State);

        // A change that breaks continuity is caught by the normal scenario validation.
        Assert.Throws<ArgumentException>(() => original.WithChangedIntervals([1],
            i => new ProductionInterval(i.Index, i.Start + TimeSpan.FromMinutes(1), i.Duration, i.State, goodCount: 9)));
        Assert.Throws<ArgumentOutOfRangeException>(() => original.WithChangedIntervals([2], i => i));
    }
}
