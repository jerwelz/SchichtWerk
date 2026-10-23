namespace SchichtWerk.Domain;

/// <summary>What the machine was doing during an interval. Every interval has exactly one state.</summary>
public enum IntervalState
{
    /// <summary>The machine produced parts.</summary>
    Running,

    /// <summary>The machine stopped during planned production time. Counts against availability.</summary>
    Downtime,

    /// <summary>Scheduled break. Excluded from planned production time.</summary>
    ScheduledBreak,
}

/// <summary>Why the machine stopped during a downtime interval.</summary>
public enum DowntimeReason
{
    MaterialShortage,
    Changeover,
    MachineFault,
}
