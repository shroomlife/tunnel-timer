namespace TunnelTimer.Core;

/// <summary>
/// What the widget shows at one instant. <see cref="TimeUntilStart"/> is negative once the
/// meeting has started.
/// </summary>
public sealed record CountdownSnapshot(CountdownPhase Phase, Meeting? Meeting, TimeSpan TimeUntilStart)
{
    public static CountdownSnapshot Hidden { get; } = new(CountdownPhase.Hidden, null, TimeSpan.Zero);
}
