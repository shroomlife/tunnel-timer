using Microsoft.UI.Dispatching;
using TunnelTimer.Core;

namespace TunnelTimer.App.Services;

/// <summary>
/// Recomputes the countdown from the clock several times a second and whenever the schedule
/// changes. Nothing is counted down, so the display cannot drift and is correct after standby.
/// </summary>
public sealed class CountdownTicker
{
    // While a countdown is on screen the digits must change on the second, so the clock is
    // read five times a second. With nothing to show, once a second is enough to notice the
    // moment a meeting enters its lead time.
    private static readonly TimeSpan VisibleInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(1);

    private readonly DispatcherQueueTimer _timer;
    private readonly MeetingSchedule _schedule;
    private readonly TimeProvider _timeProvider;

    public CountdownTicker(DispatcherQueue dispatcherQueue, MeetingSchedule schedule, TimeProvider timeProvider)
    {
        _schedule = schedule;
        _timeProvider = timeProvider;
        _schedule.Changed += (_, _) => Run(Publish);

        _timer = dispatcherQueue.CreateTimer();
        _timer.Interval = IdleInterval;
        _timer.Tick += (_, _) => Run(Tick);
    }

    public event EventHandler<CountdownSnapshot>? SnapshotUpdated;

    public void Start()
    {
        Run(Tick);
        _timer.Start();
    }

    /// <summary>
    /// One failing pass must not end the countdown. Because every pass starts again from the
    /// clock and the schedule, the next one repairs whatever this one could not show.
    /// </summary>
    private static void Run(Action pass)
    {
        try
        {
            pass();
        }
        catch (Exception exception)
        {
            AppLog.Error("Countdown pass failed", exception);
        }
    }

    private void Tick()
    {
        _schedule.RetryPendingSave();

        // RemoveStale raises Changed when it drops something, which publishes on its own.
        _schedule.RemoveStale(_timeProvider.GetLocalNow());
        Publish();
    }

    private void Publish()
    {
        var snapshot = CountdownCalculator.Calculate(_schedule.Meetings, _timeProvider.GetLocalNow());

        var interval = snapshot.Phase == CountdownPhase.Hidden ? IdleInterval : VisibleInterval;
        if (_timer.Interval != interval)
        {
            _timer.Interval = interval;
        }

        SnapshotUpdated?.Invoke(this, snapshot);
    }
}
