using SchichtWerk.Domain;
using static SchichtWerk.Tests.TestScenarios;

namespace SchichtWerk.Tests;

/// <summary>The baseline-versus-scenario differences shown in the interface.</summary>
public class ResultComparisonTests
{
    private const int Precision = 10;
    private readonly ShiftSimulation simulation = new();

    [Fact]
    public void Baseline_HasNoChanges()
    {
        var change = simulation.ChangeFromBaseline;

        Assert.False(simulation.HasChanges);
        Assert.Equal(0.0, change.OeeChange);
        Assert.Equal(0, change.GoodCountChange);
        Assert.Equal(TimeSpan.Zero, change.DowntimeChange);
    }

    [Fact]
    public void MaterialShortage_LosesEighteenGoodPartsAndTwoPercentagePoints()
    {
        simulation.ApplyMaterialShortage();
        var change = simulation.ChangeFromBaseline;

        Assert.True(simulation.HasChanges);
        Assert.Equal(-18, change.GoodCountChange);
        Assert.Equal(-18, change.TotalCountChange);
        Assert.Equal(0, change.RejectedCountChange);
        Assert.Equal(TimeSpan.FromMinutes(10), change.DowntimeChange);
        Assert.Equal(TimeSpan.FromMinutes(-10), change.RunningTimeChange);
        Assert.Equal(-18 * 0.5 / 450, change.OeeChange!.Value, Precision);   // −2.00 percentage points
        Assert.Equal(0.0, change.PerformanceChange!.Value, Precision);
    }

    [Fact]
    public void AdditionalRejects_ChangeOnlyQuality()
    {
        simulation.ApplyAdditionalRejects();
        var change = simulation.ChangeFromBaseline;

        Assert.Equal(-10, change.GoodCountChange);
        Assert.Equal(10, change.RejectedCountChange);
        Assert.Equal(0, change.TotalCountChange);
        Assert.Equal(0.0, change.AvailabilityChange);
        Assert.Equal(0.0, change.PerformanceChange);
        Assert.Equal(-10.0 / 729, change.QualityChange!.Value, Precision);
        Assert.Equal(-10 * 0.5 / 450, change.OeeChange!.Value, Precision);   // ≈ −1.11 percentage points
    }

    [Fact]
    public void BothActions_AddUp()
    {
        simulation.ApplyMaterialShortage();
        simulation.ApplyAdditionalRejects();
        var change = simulation.ChangeFromBaseline;

        Assert.Equal(-28, change.GoodCountChange);
        Assert.Equal(10, change.RejectedCountChange);
        Assert.Equal(-28 * 0.5 / 450, change.OeeChange!.Value, Precision);   // ≈ −3.11 percentage points
    }

    [Fact]
    public void UndefinedRatio_GivesUndefinedChange()
    {
        var noOutput = OeeCalculator.Calculate(Shift(Down(0)));

        var change = ResultComparison.Between(simulation.BaselineResult, noOutput);

        Assert.Null(change.QualityChange);
        Assert.Null(change.PerformanceChange);
    }
}
