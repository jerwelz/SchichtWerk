using SchichtWerk.Domain;

namespace SchichtWerk.Tests;

/// <summary>The seeded baseline must be reproducible and keep the action targets usable.</summary>
public class DemoScenarioFactoryTests
{
    private readonly ShiftScenario baseline = DemoScenarioFactory.CreateBaseline();

    [Fact]
    public void Baseline_Has96ConsecutiveFiveMinuteIntervals()
    {
        Assert.Equal(96, baseline.Intervals.Count);
        Assert.All(baseline.Intervals, i => Assert.Equal(TimeSpan.FromMinutes(5), i.Duration));
        Assert.Equal(TimeSpan.FromMinutes(480), baseline.Intervals[^1].End);
    }

    [Fact]
    public void Baseline_HasExpectedNumberOfIntervalsPerState()
    {
        Assert.Equal(81, baseline.Intervals.Count(i => i.State == IntervalState.Running));
        Assert.Equal(9, baseline.Intervals.Count(i => i.State == IntervalState.Downtime));
        Assert.Equal(6, baseline.Intervals.Count(i => i.State == IntervalState.ScheduledBreak));
    }

    [Fact]
    public void Baseline_RunningIntervalsProduceNinePiecesWithAtMostOneReject()
    {
        var running = baseline.Intervals.Where(i => i.State == IntervalState.Running).ToArray();

        Assert.All(running, i => Assert.Equal(9, i.TotalCount));
        Assert.Equal(29, running.Count(i => i.RejectedCount == 1));
        Assert.Equal(52, running.Count(i => i.RejectedCount == 0));
    }

    [Fact]
    public void ActionTargets_AreRunningRejectFreeAndDisjoint()
    {
        var targets = DemoScenarioFactory.MaterialShortageActionIntervals
            .Concat(DemoScenarioFactory.RejectActionIntervals)
            .ToArray();

        Assert.Equal(targets.Length, targets.Distinct().Count());
        Assert.All(targets, index =>
        {
            var interval = baseline.Intervals[index];
            Assert.Equal(IntervalState.Running, interval.State);
            Assert.Equal(9, interval.GoodCount);
            Assert.Equal(0, interval.RejectedCount);
        });
    }

    [Fact]
    public void ActionTargets_AreAtDocumentedTimes()
    {
        Assert.Equal(TimeSpan.FromHours(5.5), baseline.Intervals[DemoScenarioFactory.MaterialShortageActionIntervals[0]].Start); // 11:30
        Assert.Equal(TimeSpan.FromHours(3), baseline.Intervals[DemoScenarioFactory.RejectActionIntervals[0]].Start);             // 09:00
    }

    [Fact]
    public void Baseline_IsDeterministic()
    {
        var other = DemoScenarioFactory.CreateBaseline();

        Assert.Equal(baseline.Intervals, other.Intervals);
    }
}
