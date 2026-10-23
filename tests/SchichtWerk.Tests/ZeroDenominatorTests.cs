using SchichtWerk.Domain;
using static SchichtWerk.Tests.TestScenarios;

namespace SchichtWerk.Tests;

/// <summary>
/// Divisions by zero give a deliberate result: null means "not defined", 0 means "nothing achieved".
/// </summary>
public class ZeroDenominatorTests
{
    [Fact]
    public void OnlyBreak_NoPlannedTime_AllFactorsUndefined()
    {
        var result = OeeCalculator.Calculate(Shift(Break(0), Break(1)));

        Assert.Equal(TimeSpan.Zero, result.PlannedProductionTime);
        Assert.Null(result.Availability);
        Assert.Null(result.Performance);
        Assert.Null(result.Quality);
        Assert.Null(result.Oee);
        Assert.Null(result.RejectShare);
    }

    [Fact]
    public void OnlyDowntime_OeeIsZero_PerformanceAndQualityUndefined()
    {
        var result = OeeCalculator.Calculate(Shift(Down(0), Down(1)));

        Assert.Equal(0.0, result.Availability);
        Assert.Equal(0.0, result.Oee);
        Assert.Null(result.Performance);
        Assert.Null(result.Quality);
    }

    [Fact]
    public void RunningWithoutOutput_PerformanceIsZero_QualityUndefined()
    {
        var result = OeeCalculator.Calculate(Shift(Running(0, good: 0)));

        Assert.Equal(1.0, result.Availability);
        Assert.Equal(0.0, result.Performance);
        Assert.Null(result.Quality);
        Assert.Equal(0.0, result.Oee);
    }

    [Fact]
    public void OnlyRejects_QualityAndOeeAreZero()
    {
        var result = OeeCalculator.Calculate(Shift(Running(0, good: 0, rejected: 9)));

        Assert.Equal(0.0, result.Quality);
        Assert.Equal(0.0, result.Oee);
        Assert.Equal(1.0, result.RejectShare);
    }
}
