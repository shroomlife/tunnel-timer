namespace TunnelTimer.Core;

/// <summary>The open meetings, ordered by start time and persisted on every change.</summary>
public sealed class MeetingSchedule
{
    /// <summary>
    /// A meeting that started this long ago and was never acknowledged is dropped, so a
    /// forgotten entry does not greet the user with a huge overdue counter the next day.
    /// </summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(2);

    private readonly IMeetingStore _store;
    private List<Meeting> _meetings;

    public MeetingSchedule(IMeetingStore store)
    {
        _store = store;
        _meetings = Sort(store.Load());
    }

    public event EventHandler? Changed;

    public IReadOnlyList<Meeting> Meetings => _meetings;

    /// <summary>Adds a meeting. Returns <c>null</c> when the start is not in the future.</summary>
    public Meeting? Add(string title, DateTimeOffset startsAt, DateTimeOffset now, Uri? link = null)
    {
        if (startsAt <= now)
        {
            return null;
        }

        var meeting = new Meeting(Guid.NewGuid(), title.Trim(), startsAt, link);
        Apply([.. _meetings, meeting]);
        return meeting;
    }

    /// <summary>Removes a meeting, either because it was deleted or acknowledged.</summary>
    public void Remove(Guid meetingId)
    {
        if (_meetings.Any(meeting => meeting.Id == meetingId))
        {
            Apply(_meetings.Where(meeting => meeting.Id != meetingId));
        }
    }

    /// <summary>Drops meetings that started more than <see cref="StaleAfter"/> ago.</summary>
    public void RemoveStale(DateTimeOffset now)
    {
        if (_meetings.Any(meeting => IsStale(meeting, now)))
        {
            Apply(_meetings.Where(meeting => !IsStale(meeting, now)));
        }
    }

    private static bool IsStale(Meeting meeting, DateTimeOffset now) => now - meeting.StartsAt > StaleAfter;

    private static List<Meeting> Sort(IEnumerable<Meeting> meetings) =>
        [.. meetings.OrderBy(meeting => meeting.StartsAt)];

    private void Apply(IEnumerable<Meeting> meetings)
    {
        _meetings = Sort(meetings);
        _store.Save(_meetings);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
