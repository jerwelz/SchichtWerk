namespace SchichtWerk.Domain;

/// <summary>
/// One time slice of the shift with its state and the parts produced in it.
/// Immutable: changes create a new interval through the validating constructor.
/// </summary>
public sealed record ProductionInterval
{
    public ProductionInterval(
        int index,
        TimeSpan start,
        TimeSpan duration,
        IntervalState state,
        DowntimeReason? reason = null,
        int goodCount = 0,
        int rejectedCount = 0)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Index must not be negative.");
        if (start < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(start), "Start must not be negative.");
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be positive.");
        if (!Enum.IsDefined(state))
            throw new ArgumentOutOfRangeException(nameof(state), $"Unknown state {state}.");
        if (goodCount < 0)
            throw new ArgumentOutOfRangeException(nameof(goodCount), "Good count must not be negative.");
        if (rejectedCount < 0)
            throw new ArgumentOutOfRangeException(nameof(rejectedCount), "Rejected count must not be negative.");

        if (state == IntervalState.Downtime && reason is null)
            throw new ArgumentException($"Downtime interval {index} needs a reason.", nameof(reason));
        if (state != IntervalState.Downtime && reason is not null)
            throw new ArgumentException($"Only downtime intervals have a reason (interval {index}).", nameof(reason));
        if (reason is { } r && !Enum.IsDefined(r))
            throw new ArgumentOutOfRangeException(nameof(reason), $"Unknown downtime reason {r}.");

        // Simplified model: a stopped machine or a break produces nothing.
        if (state != IntervalState.Running && goodCount + rejectedCount > 0)
            throw new ArgumentException($"Interval {index} is not running and cannot contain parts.");

        Index = index;
        Start = start;
        Duration = duration;
        State = state;
        Reason = reason;
        GoodCount = goodCount;
        RejectedCount = rejectedCount;
    }

    /// <summary>Position in the shift, starting at 0.</summary>
    public int Index { get; }

    /// <summary>Offset from the start of the shift.</summary>
    public TimeSpan Start { get; }

    public TimeSpan Duration { get; }

    public TimeSpan End => Start + Duration;

    public IntervalState State { get; }

    /// <summary>Set only for downtime intervals.</summary>
    public DowntimeReason? Reason { get; }

    public int GoodCount { get; }

    public int RejectedCount { get; }

    /// <summary>Derived, so good + rejected = total always holds.</summary>
    public int TotalCount => GoodCount + RejectedCount;

    /// <summary>
    /// Returns this time slot as downtime. The parts it would have produced are lost.
    /// Only running intervals can stop; a break is not part of planned production time.
    /// </summary>
    public ProductionInterval ToDowntime(DowntimeReason reason)
    {
        if (State != IntervalState.Running)
            throw new InvalidOperationException($"Only a running interval can become downtime (interval {Index} is {State}).");

        return new ProductionInterval(Index, Start, Duration, IntervalState.Downtime, reason);
    }

    /// <summary>Returns this interval with <paramref name="count"/> good parts counted as rejected. The total stays the same.</summary>
    public ProductionInterval WithAdditionalRejects(int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive.");
        if (count > GoodCount)
            throw new InvalidOperationException($"Interval {Index} has only {GoodCount} good parts.");

        return new ProductionInterval(Index, Start, Duration, State, Reason, GoodCount - count, RejectedCount + count);
    }
}
