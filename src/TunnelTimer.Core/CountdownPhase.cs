namespace TunnelTimer.Core;

public enum CountdownPhase
{
    /// <summary>No meeting is close enough to show the widget.</summary>
    Hidden,

    /// <summary>A meeting starts within the lead time.</summary>
    Upcoming,

    /// <summary>A meeting starts within the imminent threshold.</summary>
    Imminent,

    /// <summary>The meeting has started and was not acknowledged yet.</summary>
    Overdue,
}
