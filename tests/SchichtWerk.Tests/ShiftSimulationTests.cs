using SchichtWerk.Domain;

namespace SchichtWerk.Tests;

/// <summary>The two scenario actions, their combination and reset, checked against the hand calculation.</summary>
public class ShiftSimulationTests
{
    private const int Precision = 10;
    private readonly ShiftSimulation simulation = new();

    [Fact]
    public void NewSimulation_StartsAtBaseline()
    {
        Assert.Same(simulation.Baseline, simulation.Current);
        Assert.Equal(700, simulation.CurrentResult.GoodCount);
        Assert.False(simulation.MaterialShortageApplied);
        Assert.False(simulation.AdditionalRejectsApplied);
    }

    [Fact]
    public void MaterialShortage_TurnsTenRunningMinutesIntoDowntime()
    {
        simulation.ApplyMaterialShortage();
        var result = simulation.CurrentResult;

        Assert.Equal(TimeSpan.FromMinutes(450), result.PlannedProductionTime);
        Assert.Equal(TimeSpan.FromMinutes(395), result.RunningTime);
        Assert.Equal(TimeSpan.FromMinutes(55), result.DowntimeTime);
        Assert.Equal(711, result.TotalCount);
        Assert.Equal(682, result.GoodCount);   // 18 good parts from two all-good intervals are lost
        Assert.Equal(29, result.RejectedCount);
        Assert.Equal(682 * 0.5 / 450, result.Oee!.Value, Precision);    // ≈ 75.78 %
        Assert.Equal(0.9, result.Performance!.Value, Precision);        // speed while running is unchanged
        Assert.Equal(-2.0, Math.Round((result.Oee.Value - simulation.BaselineResult.Oee!.Value) * 100, 2));
    }

    [Fact]
    public void MaterialShortage_IsBookedUnderMaterialShortage()
    {
        simulation.ApplyMaterialShortage();

        var materialShortage = simulation.CurrentResult.DowntimeByReason
            .Single(d => d.Reason == DowntimeReason.MaterialShortage);
        Assert.Equal(TimeSpan.FromMinutes(30), materialShortage.Duration);
        Assert.All(DemoScenarioFactory.MaterialShortageActionIntervals, index =>
            Assert.Equal(DowntimeReason.MaterialShortage, simulation.Current.Intervals[index].Reason));
    }

    [Fact]
    public void AdditionalRejects_ReclassifiesTenGoodPartsWithoutChangingOutputOrTime()
    {
        simulation.ApplyAdditionalRejects();
        var result = simulation.CurrentResult;

        Assert.Equal(729, result.TotalCount);
        Assert.Equal(690, result.GoodCount);
        Assert.Equal(39, result.RejectedCount);
        Assert.Equal(TimeSpan.FromMinutes(405), result.RunningTime);
        Assert.Equal(TimeSpan.FromMinutes(45), result.DowntimeTime);
        Assert.Equal(simulation.BaselineResult.Availability, result.Availability);
        Assert.Equal(simulation.BaselineResult.Performance, result.Performance);
        Assert.Equal(690.0 / 729, result.Quality!.Value, Precision);
        Assert.Equal(690 * 0.5 / 450, result.Oee!.Value, Precision);    // ≈ 76.67 %
    }

    [Fact]
    public void BothActions_CombineToExpectedResult()
    {
        simulation.ApplyMaterialShortage();
        simulation.ApplyAdditionalRejects();
        var result = simulation.CurrentResult;

        Assert.Equal(711, result.TotalCount);
        Assert.Equal(672, result.GoodCount);
        Assert.Equal(39, result.RejectedCount);
        Assert.Equal(TimeSpan.FromMinutes(55), result.DowntimeTime);
        Assert.Equal(672 * 0.5 / 450, result.Oee!.Value, Precision);    // ≈ 74.67 %
        Assert.True(simulation.MaterialShortageApplied);
        Assert.True(simulation.AdditionalRejectsApplied);
    }

    [Fact]
    public void BothActions_OrderDoesNotMatter()
    {
        var other = new ShiftSimulation();

        simulation.ApplyMaterialShortage();
        simulation.ApplyAdditionalRejects();
        other.ApplyAdditionalRejects();
        other.ApplyMaterialShortage();

        Assert.Equal(simulation.Current.Intervals, other.Current.Intervals);
        Assert.Equal(simulation.CurrentResult.Oee, other.CurrentResult.Oee);
    }

    [Fact]
    public void MaterialShortage_CannotBeAppliedTwice()
    {
        simulation.ApplyMaterialShortage();
        var before = simulation.Current;

        Assert.Throws<InvalidOperationException>(simulation.ApplyMaterialShortage);
        Assert.Same(before, simulation.Current);
        Assert.Equal(682, simulation.CurrentResult.GoodCount);
    }

    [Fact]
    public void AdditionalRejects_CannotBeAppliedTwice()
    {
        simulation.ApplyAdditionalRejects();
        var before = simulation.Current;

        Assert.Throws<InvalidOperationException>(simulation.ApplyAdditionalRejects);
        Assert.Same(before, simulation.Current);
        Assert.Equal(39, simulation.CurrentResult.RejectedCount);
    }

    [Fact]
    public void Reset_RestoresBaselineAndAllowsActionsAgain()
    {
        simulation.ApplyMaterialShortage();
        simulation.ApplyAdditionalRejects();

        simulation.Reset();

        Assert.Equal(DemoScenarioFactory.CreateBaseline().Intervals, simulation.Current.Intervals);
        Assert.Equal(simulation.BaselineResult, simulation.CurrentResult);
        Assert.Equal(700, simulation.CurrentResult.GoodCount);
        Assert.False(simulation.MaterialShortageApplied);
        Assert.False(simulation.AdditionalRejectsApplied);

        simulation.ApplyMaterialShortage();
        Assert.Equal(682, simulation.CurrentResult.GoodCount);
    }

    [Fact]
    public void Actions_DoNotChangeTheBaseline()
    {
        simulation.ApplyMaterialShortage();
        simulation.ApplyAdditionalRejects();

        Assert.Equal(DemoScenarioFactory.CreateBaseline().Intervals, simulation.Baseline.Intervals);
        Assert.Equal(700, simulation.BaselineResult.GoodCount);
    }

    public static TheoryData<string> AllStates => ["baseline", "shortage", "rejects", "both"];

    [Theory]
    [MemberData(nameof(AllStates))]
    public void Invariants_HoldInEveryScenario(string state)
    {
        if (state is "shortage" or "both") simulation.ApplyMaterialShortage();
        if (state is "rejects" or "both") simulation.ApplyAdditionalRejects();
        var result = simulation.CurrentResult;
        var intervals = simulation.Current.Intervals;

        Assert.Equal(96, intervals.Count);
        Assert.Equal(TimeSpan.FromMinutes(480), result.ShiftTime);
        Assert.Equal(TimeSpan.FromMinutes(450), result.PlannedProductionTime);
        Assert.Equal(result.PlannedProductionTime, result.RunningTime + result.DowntimeTime);
        Assert.Equal(result.TotalCount, result.GoodCount + result.RejectedCount);
        Assert.Equal(result.DowntimeTime, TimeSpan.FromTicks(result.DowntimeByReason.Sum(d => d.Duration.Ticks)));
        Assert.Equal(result.GoodCount, result.Cumulative[^1].GoodCount);
        Assert.Equal(
            result.Oee!.Value,
            result.Availability!.Value * result.Performance!.Value * result.Quality!.Value,
            Precision);
        Assert.InRange(result.Performance.Value, 0, 1);
    }
}
