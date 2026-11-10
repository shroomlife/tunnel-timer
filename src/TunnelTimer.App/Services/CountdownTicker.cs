using Microsoft.UI.Dispatching;
using TunnelTimer.Core;

namespace TunnelTimer.App.Services;

/// <summary>
/// Recomputes the countdown from the clock several times a second and whenever the schedule
/// changes. Nothing is counted down, so the display cannot drift and is correct after standby.
/// </summary>
public sealed class CountdownTicker
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(200);

    private readonly DispatcherQueueTimer _timer;
    private readonly MeetingSchedule _schedule;
    private readonly TimeProvider _timeProvider;

    public CountdownTicker(DispatcherQueue dispatcherQueue, MeetingSchedule schedule, TimeProvider timeProvider)
    {
        _schedule = schedule;
        _timeProvider = timeProvider;
        _schedule.Changed += (_, _) => Publish();

        _timer = dispatcherQueue.CreateTimer();
        _timer.Interval = Interval;
        _timer.Tick += (_, _) => Tick();
    }

    public event EventHandler<CountdownSnapshot>? SnapshotUpdated;

    public void Start()
    {
        Tick();
        _timer.Start();
    }

    private void Tick()
    {
        // RemoveStale raises Changed when it drops something, which publishes on its own.
        _schedule.RemoveStale(_timeProvider.GetLocalNow());
        Publish();
    }

    private void Publish()
    {
        var snapshot = CountdownCalculator.Calculate(_schedule.Meetings, _timeProvider.GetLocalNow());
        SnapshotUpdated?.Invoke(this, snapshot);
    }
}
