namespace SchichtWerk.Domain;

/// <summary>Descriptive information about the fictional shift. Not used in calculations.</summary>
public sealed record ShiftInfo(string Machine, string Product, string ShiftName, TimeOnly StartTime);

/// <summary>
/// A complete, validated shift: ordered intervals that cover the shift without gaps or overlaps,
/// plus the ideal cycle time of the single product.
/// </summary>
public sealed class ShiftScenario
{
    public ShiftScenario(
        ShiftInfo info,
        TimeSpan shiftLength,
        TimeSpan idealCycleTime,
        IEnumerable<ProductionInterval> intervals)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(intervals);
        if (shiftLength <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(shiftLength), "Shift length must be positive.");
        if (idealCycleTime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(idealCycleTime), "Ideal cycle time must be positive.");

        var list = intervals.ToArray();
        if (list.Length == 0)
            throw new ArgumentException("A shift needs at least one interval.", nameof(intervals));

        var expectedStart = TimeSpan.Zero;
        for (var i = 0; i < list.Length; i++)
        {
            var interval = list[i] ?? throw new ArgumentException($"Interval {i} is missing.", nameof(intervals));

            if (interval.Index != i)
                throw new ArgumentException($"Interval at position {i} has index {interval.Index}; intervals must be ordered.", nameof(intervals));
            if (interval.Start < expectedStart)
                throw new ArgumentException($"Interval {i} overlaps the previous interval.", nameof(intervals));
            if (interval.Start > expectedStart)
                throw new ArgumentException($"Gap before interval {i}.", nameof(intervals));

            // Ideal cycle time is the fastest possible speed, so more parts than
            // duration / cycle time would mean a performance above 100 %.
            // Compared in ticks (integers) to avoid floating-point rounding.
            if ((long)interval.TotalCount * idealCycleTime.Ticks > interval.Duration.Ticks)
                throw new ArgumentException(
                    $"Interval {i}: {interval.TotalCount} parts in {interval.Duration.TotalMinutes} min exceed the ideal cycle time.",
                    nameof(intervals));

            expectedStart = interval.End;
        }

        if (expectedStart != shiftLength)
            throw new ArgumentException(
                $"Intervals cover {expectedStart.TotalMinutes} min, but the shift is {shiftLength.TotalMinutes} min long.",
                nameof(intervals));

        Info = info;
        ShiftLength = shiftLength;
        IdealCycleTime = idealCycleTime;
        Intervals = Array.AsReadOnly(list);
    }

    public ShiftInfo Info { get; }

    public TimeSpan ShiftLength { get; }

    /// <summary>Fastest possible time to produce one piece.</summary>
    public TimeSpan IdealCycleTime { get; }

    public IReadOnlyList<ProductionInterval> Intervals { get; }

    /// <summary>
    /// Returns a new scenario in which the given intervals are replaced by <paramref name="change"/>.
    /// The result passes through the same validation as any other scenario; this one is unchanged.
    /// </summary>
    public ShiftScenario WithChangedIntervals(IEnumerable<int> indices, Func<ProductionInterval, ProductionInterval> change)
    {
        ArgumentNullException.ThrowIfNull(indices);
        ArgumentNullException.ThrowIfNull(change);

        var changed = Intervals.ToArray();
        foreach (var index in indices)
        {
            if (index < 0 || index >= changed.Length)
                throw new ArgumentOutOfRangeException(nameof(indices), $"There is no interval {index}.");

            changed[index] = change(changed[index]);
        }

        return new ShiftScenario(Info, ShiftLength, IdealCycleTime, changed);
    }
}
