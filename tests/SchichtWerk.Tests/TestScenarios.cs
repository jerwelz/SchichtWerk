using SchichtWerk.Domain;

namespace SchichtWerk.Tests;

/// <summary>Builds small custom shifts of consecutive 5-minute intervals for edge-case tests.</summary>
internal static class TestScenarios
{
    public static readonly TimeSpan Five = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Cycle = TimeSpan.FromSeconds(30);
    private static readonly ShiftInfo Info = new("Testmaschine", "Testartikel", "Testschicht", new TimeOnly(6, 0));

    public static ProductionInterval Running(int index, int good, int rejected = 0) =>
        new(index, index * Five, Five, IntervalState.Running, goodCount: good, rejectedCount: rejected);

    public static ProductionInterval Down(int index, DowntimeReason reason = DowntimeReason.MachineFault) =>
        new(index, index * Five, Five, IntervalState.Downtime, reason);

    public static ProductionInterval Break(int index) =>
        new(index, index * Five, Five, IntervalState.ScheduledBreak);

    public static ShiftScenario Shift(params ProductionInterval[] intervals) =>
        new(Info, intervals.Length * Five, Cycle, intervals);
}
