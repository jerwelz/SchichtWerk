namespace SchichtWerk.Domain;

/// <summary>Total downtime caused by one reason.</summary>
public sealed record DowntimeByReason(DowntimeReason Reason, TimeSpan Duration);

/// <summary>
/// Cumulative values at the end of an interval (or at shift start for the first point).
/// TheoreticalCount is what the machine could have produced at ideal speed with no stops.
/// </summary>
public sealed record CumulativePoint(TimeSpan Time, int GoodCount, double TheoreticalCount);

/// <summary>
/// Read-only results of an OEE calculation. Ratios are unrounded values between 0 and 1;
/// null means "not defined" (division by zero), which is different from 0.
/// </summary>
public sealed record OeeResult
{
    public required TimeSpan ShiftTime { get; init; }
    public required TimeSpan ScheduledBreakTime { get; init; }

    /// <summary>Shift time minus scheduled breaks. The OEE denominator.</summary>
    public required TimeSpan PlannedProductionTime { get; init; }

    public required TimeSpan RunningTime { get; init; }
    public required TimeSpan DowntimeTime { get; init; }

    public required int TotalCount { get; init; }
    public required int GoodCount { get; init; }
    public required int RejectedCount { get; init; }

    /// <summary>Pieces possible in the planned production time at ideal cycle time.</summary>
    public required double TheoreticalCapacity { get; init; }

    /// <summary>Running time / planned production time.</summary>
    public required double? Availability { get; init; }

    /// <summary>Ideal cycle time × total count / running time.</summary>
    public required double? Performance { get; init; }

    /// <summary>Good count / total count.</summary>
    public required double? Quality { get; init; }

    /// <summary>Good count × ideal cycle time / planned production time (= A × P × Q).</summary>
    public required double? Oee { get; init; }

    /// <summary>Rejected count / total count.</summary>
    public required double? RejectShare { get; init; }

    /// <summary>One entry per downtime reason, including reasons with zero minutes.</summary>
    public required IReadOnlyList<DowntimeByReason> DowntimeByReason { get; init; }

    /// <summary>Starts at time 0 and has one point per interval end.</summary>
    public required IReadOnlyList<CumulativePoint> Cumulative { get; init; }
}
