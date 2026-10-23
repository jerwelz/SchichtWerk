using SchichtWerk.Domain;

namespace SchichtWerk.Tests;

/// <summary>The hand-calculated baseline from the specification must come out of the model.</summary>
public class BaselineCalculationTests
{
    private const int Precision = 10;
    private readonly OeeResult result = OeeCalculator.Calculate(DemoScenarioFactory.CreateBaseline());

    [Fact]
    public void Times_MatchSpecification()
    {
        Assert.Equal(TimeSpan.FromMinutes(480), result.ShiftTime);
        Assert.Equal(TimeSpan.FromMinutes(30), result.ScheduledBreakTime);
        Assert.Equal(TimeSpan.FromMinutes(450), result.PlannedProductionTime);
        Assert.Equal(TimeSpan.FromMinutes(405), result.RunningTime);
        Assert.Equal(TimeSpan.FromMinutes(45), result.DowntimeTime);
    }

    [Fact]
    public void Counts_MatchSpecification()
    {
        Assert.Equal(729, result.TotalCount);
        Assert.Equal(700, result.GoodCount);
        Assert.Equal(29, result.RejectedCount);
    }

    [Fact]
    public void OeeFactors_MatchHandCalculation()
    {
        Assert.Equal(405.0 / 450, result.Availability!.Value, Precision);           // 90 %
        Assert.Equal(0.5 * 729 / 405, result.Performance!.Value, Precision);         // 90 %
        Assert.Equal(700.0 / 729, result.Quality!.Value, Precision);                 // ≈ 96.02 %
        Assert.Equal(700 * 0.5 / 450, result.Oee!.Value, Precision);                 // ≈ 77.78 %
        Assert.Equal(0.7778, Math.Round(result.Oee.Value, 4));
    }

    [Fact]
    public void Oee_EqualsProductOfFactors()
    {
        var product = result.Availability!.Value * result.Performance!.Value * result.Quality!.Value;

        Assert.Equal(result.Oee!.Value, product, Precision);
    }

    [Fact]
    public void RejectShare_IsRejectsOverTotal()
    {
        Assert.Equal(29.0 / 729, result.RejectShare!.Value, Precision);
        Assert.Equal("4.0", (result.RejectShare.Value * 100).ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TheoreticalCapacity_ConvertsMinutesAndSecondsCorrectly()
    {
        // 450 min = 27,000 s; 27,000 s / 30 s per piece = 900 pieces
        Assert.Equal(900, result.TheoreticalCapacity, Precision);
    }

    [Fact]
    public void DowntimeByReason_AddsUpToTotalDowntime()
    {
        Assert.Equal(
            [
                new DowntimeByReason(DowntimeReason.MaterialShortage, TimeSpan.FromMinutes(20)),
                new DowntimeByReason(DowntimeReason.Changeover, TimeSpan.FromMinutes(15)),
                new DowntimeByReason(DowntimeReason.MachineFault, TimeSpan.FromMinutes(10)),
            ],
            result.DowntimeByReason);
        Assert.Equal(result.DowntimeTime, TimeSpan.FromTicks(result.DowntimeByReason.Sum(d => d.Duration.Ticks)));
    }

    [Fact]
    public void Cumulative_StartsAtZeroAndEndsAtTotals()
    {
        Assert.Equal(97, result.Cumulative.Count);
        Assert.Equal(new CumulativePoint(TimeSpan.Zero, 0, 0), result.Cumulative[0]);

        var last = result.Cumulative[^1];
        Assert.Equal(TimeSpan.FromMinutes(480), last.Time);
        Assert.Equal(700, last.GoodCount);
        Assert.Equal(900, last.TheoreticalCount, Precision);
    }

    [Fact]
    public void Cumulative_IsFlatDuringBreakAndNeverDecreases()
    {
        // Break 10:00–10:30 = minutes 240–270 = points 48..54
        var atBreakStart = result.Cumulative[48];
        var atBreakEnd = result.Cumulative[54];
        Assert.Equal(atBreakStart.GoodCount, atBreakEnd.GoodCount);
        Assert.Equal(atBreakStart.TheoreticalCount, atBreakEnd.TheoreticalCount);

        for (var i = 1; i < result.Cumulative.Count; i++)
        {
            Assert.True(result.Cumulative[i].GoodCount >= result.Cumulative[i - 1].GoodCount);
            Assert.True(result.Cumulative[i].GoodCount <= result.Cumulative[i].TheoreticalCount);
        }
    }
}
