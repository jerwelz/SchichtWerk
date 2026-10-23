using System.Globalization;
using SchichtWerk.Domain;

namespace SchichtWerk.Web.Charts;

/// <summary>
/// Small helpers for drawing. Charts use data units directly: x = minutes since shift start,
/// y = pieces. The SVG stretches to its box; HTML labels are placed by percentage.
/// </summary>
public static class ChartMath
{
    /// <summary>Number for SVG attributes and CSS (always with a "." decimal point).</summary>
    public static string Css(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Fraction of the total as a CSS percentage, e.g. "62.5%".</summary>
    public static string Percent(double part, double total) => Css(part / total * 100) + "%";

    /// <summary>Clock-hour ticks every <paramref name="stepHours"/> hours, including the shift end.</summary>
    public static IReadOnlyList<TimeSpan> TimeTicks(TimeSpan shiftLength, int stepHours = 2)
    {
        var ticks = new List<TimeSpan>();
        for (var t = TimeSpan.Zero; t <= shiftLength; t += TimeSpan.FromHours(stepHours))
            ticks.Add(t);
        return ticks;
    }

    /// <summary>Rounds the y-axis maximum up to a multiple of <paramref name="step"/>.</summary>
    public static int AxisMax(double value, int step) => (int)Math.Ceiling(value / step) * step;
}

/// <summary>Consecutive intervals with the same state, merged for the timeline.</summary>
public sealed record TimelineSegment(TimeSpan Start, TimeSpan End, IntervalState State, DowntimeReason? Reason, bool ChangedByScenario)
{
    public TimeSpan Duration => End - Start;

    public static IReadOnlyList<TimelineSegment> Build(ShiftScenario current, ShiftScenario baseline)
    {
        var segments = new List<TimelineSegment>();
        foreach (var interval in current.Intervals)
        {
            var changed = interval.State != baseline.Intervals[interval.Index].State;

            if (segments.Count > 0 && segments[^1] is var last
                && last.State == interval.State && last.Reason == interval.Reason && last.ChangedByScenario == changed)
            {
                segments[^1] = last with { End = interval.End };
            }
            else
            {
                segments.Add(new TimelineSegment(interval.Start, interval.End, interval.State, interval.Reason, changed));
            }
        }
        return segments;
    }
}
