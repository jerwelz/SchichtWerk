using SchichtWerk.Domain;

namespace SchichtWerk.Web.Formatting;

/// <summary>German display names for domain enums.</summary>
public static class Labels
{
    public static string For(DowntimeReason reason) => reason switch
    {
        DowntimeReason.MaterialShortage => "Materialmangel",
        DowntimeReason.Changeover => "Umrüsten",
        DowntimeReason.MachineFault => "Maschinenstörung",
        _ => reason.ToString(),
    };

    public static string For(IntervalState state) => state switch
    {
        IntervalState.Running => "Produktion",
        IntervalState.Downtime => "Stillstand",
        IntervalState.ScheduledBreak => "Pause",
        _ => state.ToString(),
    };

    /// <summary>"Stillstand: Materialmangel" or just "Produktion".</summary>
    public static string For(ProductionInterval interval) =>
        interval.Reason is { } reason ? $"{For(interval.State)}: {For(reason)}" : For(interval.State);
}
