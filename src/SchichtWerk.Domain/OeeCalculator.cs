namespace SchichtWerk.Domain;

/// <summary>
/// Calculates OEE and production totals from a validated shift.
/// Pure function: the same scenario always gives the same result, and nothing is rounded.
/// </summary>
public static class OeeCalculator
{
    public static OeeResult Calculate(ShiftScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var intervals = scenario.Intervals;
        var cycle = scenario.IdealCycleTime;

        var shiftTime = Sum(intervals);
        var breakTime = Sum(intervals.Where(i => i.State == IntervalState.ScheduledBreak));
        var runningTime = Sum(intervals.Where(i => i.State == IntervalState.Running));
        var downtimeTime = Sum(intervals.Where(i => i.State == IntervalState.Downtime));
        var plannedTime = shiftTime - breakTime;

        var good = intervals.Sum(i => i.GoodCount);
        var rejected = intervals.Sum(i => i.RejectedCount);
        var total = good + rejected;

        // Durations are divided in ticks, so seconds and minutes are never mixed.
        double? availability = plannedTime > TimeSpan.Zero ? Ratio(runningTime.Ticks, plannedTime.Ticks) : null;
        double? performance = runningTime > TimeSpan.Zero ? Ratio(total * cycle.Ticks, runningTime.Ticks) : null;
        double? quality = total > 0 ? Ratio(good, total) : null;
        double? rejectShare = total > 0 ? Ratio(rejected, total) : null;

        // Equivalent to availability × performance × quality, but still defined (as 0)
        // when the machine never ran and performance/quality are undefined.
        double? oee = plannedTime > TimeSpan.Zero ? Ratio(good * cycle.Ticks, plannedTime.Ticks) : null;

        // ShiftScenario rejects intervals faster than the ideal cycle time, so this cannot
        // happen for a valid scenario. It guards against silently showing more than 100 %.
        if (performance > 1.0)
            throw new InvalidOperationException($"Performance of {performance:P2} is impossible.");

        var downtimeByReason = Enum.GetValues<DowntimeReason>()
            .Select(reason => new DowntimeByReason(
                reason,
                Sum(intervals.Where(i => i.State == IntervalState.Downtime && i.Reason == reason))))
            .ToArray();

        return new OeeResult
        {
            ShiftTime = shiftTime,
            ScheduledBreakTime = breakTime,
            PlannedProductionTime = plannedTime,
            RunningTime = runningTime,
            DowntimeTime = downtimeTime,
            TotalCount = total,
            GoodCount = good,
            RejectedCount = rejected,
            TheoreticalCapacity = Ratio(plannedTime.Ticks, cycle.Ticks),
            Availability = availability,
            Performance = performance,
            Quality = quality,
            Oee = oee,
            RejectShare = rejectShare,
            DowntimeByReason = downtimeByReason,
            Cumulative = BuildCumulative(intervals, cycle),
        };
    }

    private static CumulativePoint[] BuildCumulative(IReadOnlyList<ProductionInterval> intervals, TimeSpan cycle)
    {
        var points = new CumulativePoint[intervals.Count + 1];
        points[0] = new CumulativePoint(TimeSpan.Zero, 0, 0);

        var good = 0;
        var plannedTicks = 0L;
        for (var i = 0; i < intervals.Count; i++)
        {
            var interval = intervals[i];
            good += interval.GoodCount;
            if (interval.State != IntervalState.ScheduledBreak)
                plannedTicks += interval.Duration.Ticks;

            points[i + 1] = new CumulativePoint(interval.End, good, Ratio(plannedTicks, cycle.Ticks));
        }

        return points;
    }

    private static TimeSpan Sum(IEnumerable<ProductionInterval> intervals) =>
        TimeSpan.FromTicks(intervals.Sum(i => i.Duration.Ticks));

    private static double Ratio(long numerator, long denominator) => (double)numerator / denominator;
}
