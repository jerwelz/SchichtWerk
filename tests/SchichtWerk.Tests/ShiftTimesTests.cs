using SchichtWerk.Domain;

namespace SchichtWerk.Tests;

public class ShiftTimesTests
{
    [Fact]
    public void PlannedProductionTime_ExcludesScheduledBreak()
    {
        Assert.Equal(TimeSpan.FromMinutes(450), ShiftTimes.PlannedProductionTime);
    }

    [Fact]
    public void TheoreticalCapacity_ConvertsSecondsAndMinutesCorrectly()
    {
        // 450 min = 27,000 s; 27,000 s / 30 s per piece = 900 pieces
        Assert.Equal(900, ShiftTimes.TheoreticalCapacity);
    }
}
