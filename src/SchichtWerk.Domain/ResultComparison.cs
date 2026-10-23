namespace SchichtWerk.Domain;

/// <summary>
/// Difference between a scenario result and the baseline (current − baseline).
/// Ratio changes are fractions (−0.02 = −2 percentage points); null if either side is undefined.
/// </summary>
public sealed record ResultComparison(
    double? OeeChange,
    double? AvailabilityChange,
    double? PerformanceChange,
    double? QualityChange,
    double? RejectShareChange,
    int TotalCountChange,
    int GoodCountChange,
    int RejectedCountChange,
    TimeSpan RunningTimeChange,
    TimeSpan DowntimeChange)
{
    public static ResultComparison Between(OeeResult baseline, OeeResult current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        return new ResultComparison(
            current.Oee - baseline.Oee,
            current.Availability - baseline.Availability,
            current.Performance - baseline.Performance,
            current.Quality - baseline.Quality,
            current.RejectShare - baseline.RejectShare,
            current.TotalCount - baseline.TotalCount,
            current.GoodCount - baseline.GoodCount,
            current.RejectedCount - baseline.RejectedCount,
            current.RunningTime - baseline.RunningTime,
            current.DowntimeTime - baseline.DowntimeTime);
    }
}
