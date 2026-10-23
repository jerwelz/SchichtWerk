namespace SchichtWerk.Domain;

/// <summary>
/// Fixed time parameters of the fictional demo shift.
/// Milestone 1 placeholder; replaced by the interval model in Milestone 2.
/// </summary>
public static class ShiftTimes
{
    public static readonly TimeSpan ShiftLength = TimeSpan.FromMinutes(480);
    public static readonly TimeSpan ScheduledBreak = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan IdealCycleTime = TimeSpan.FromSeconds(30);

    public static TimeSpan PlannedProductionTime => ShiftLength - ScheduledBreak;

    /// <summary>Pieces that could be produced in the planned time at ideal speed.</summary>
    public static int TheoreticalCapacity => (int)(PlannedProductionTime.Ticks / IdealCycleTime.Ticks);
}
