namespace TunnelTimer.Core;

public static class CountdownCalculator
{
    /// <summary>How long before the start the widget appears.</summary>
    public static readonly TimeSpan LeadTime = TimeSpan.FromMinutes(10);

    /// <summary>How long before the start the widget switches to the imminent look.</summary>
    public static readonly TimeSpan ImminentThreshold = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Picks the earliest meeting and derives the widget state from the current time. The
    /// state is recomputed from the clock on every call, so it stays correct after standby.
    /// </summary>
    public static CountdownSnapshot Calculate(IEnumerable<Meeting> meetings, DateTimeOffset now)
    {
        var next = meetings.MinBy(meeting => meeting.StartsAt);
        if (next is null)
        {
            return CountdownSnapshot.Hidden;
        }

        var timeUntilStart = next.StartsAt - now;
        if (timeUntilStart > LeadTime)
        {
            return CountdownSnapshot.Hidden;
        }

        var phase = timeUntilStart <= TimeSpan.Zero
            ? CountdownPhase.Overdue
            : timeUntilStart <= ImminentThreshold
                ? CountdownPhase.Imminent
                : CountdownPhase.Upcoming;

        return new CountdownSnapshot(phase, next, timeUntilStart);
    }
}
