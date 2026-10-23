namespace SchichtWerk.Domain;

/// <summary>
/// Builds the fixed, fictional baseline shift. The values are invented for the demo
/// and internally consistent; they are not industry benchmarks.
/// </summary>
/// <remarks>
/// 96 intervals of 5 minutes (06:00–14:00). Downtime and the break are placed at fixed times;
/// every running interval produces 9 pieces at an ideal cycle time of 30 s (max. 10 per interval).
/// </remarks>
public static class DemoScenarioFactory
{
    public const int IntervalCount = 96;
    public const int PiecesPerRunningInterval = 9;
    public const int BaselineRejectCount = 29;

    public static readonly TimeSpan IntervalLength = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan IdealCycleTime = TimeSpan.FromSeconds(30);
    public static readonly ShiftInfo Info = new("Presse P-01", "Artikel A-100", "Frühschicht", new TimeOnly(6, 0));

    /// <summary>11:30–11:40. Running and reject-free in the baseline; target of the material-shortage action.</summary>
    public static readonly IReadOnlyList<int> MaterialShortageActionIntervals = [66, 67];

    /// <summary>09:00–09:10. Running and reject-free in the baseline; target of the reject action.</summary>
    public static readonly IReadOnlyList<int> RejectActionIntervals = [36, 37];

    public static ShiftScenario CreateBaseline()
    {
        var rejectIntervals = ChooseBaselineRejectIntervals();

        var intervals = Enumerable.Range(0, IntervalCount).Select(index =>
        {
            var start = index * IntervalLength;
            var (state, reason) = BaselineStateAt(start);
            if (state != IntervalState.Running)
                return new ProductionInterval(index, start, IntervalLength, state, reason);

            var rejected = rejectIntervals.Contains(index) ? 1 : 0;
            return new ProductionInterval(index, start, IntervalLength, state,
                goodCount: PiecesPerRunningInterval - rejected, rejectedCount: rejected);
        });

        return new ShiftScenario(Info, IntervalCount * IntervalLength, IdealCycleTime, intervals);
    }

    /// <summary>Fixed timetable of the baseline (times relative to 06:00).</summary>
    private static (IntervalState State, DowntimeReason? Reason) BaselineStateAt(TimeSpan start) => start.TotalMinutes switch
    {
        < 15 => (IntervalState.Downtime, DowntimeReason.Changeover),                // 06:00–06:15
        >= 140 and < 150 => (IntervalState.Downtime, DowntimeReason.MachineFault),  // 08:20–08:30
        >= 240 and < 270 => (IntervalState.ScheduledBreak, null),                   // 10:00–10:30
        >= 360 and < 380 => (IntervalState.Downtime, DowntimeReason.MaterialShortage), // 12:00–12:20
        _ => (IntervalState.Running, null),
    };

    /// <summary>
    /// Spreads the 29 baseline rejects evenly over the running intervals that are not
    /// reserved for a scenario action (one reject per chosen interval).
    /// </summary>
    private static HashSet<int> ChooseBaselineRejectIntervals()
    {
        var eligible = Enumerable.Range(0, IntervalCount)
            .Where(i => BaselineStateAt(i * IntervalLength).State == IntervalState.Running)
            .Except(MaterialShortageActionIntervals)
            .Except(RejectActionIntervals)
            .ToArray();

        // Integer division: interval k is chosen whenever k * 29 / n reaches the next whole number.
        return eligible
            .Where((_, k) => (k + 1) * BaselineRejectCount / eligible.Length > k * BaselineRejectCount / eligible.Length)
            .ToHashSet();
    }
}
