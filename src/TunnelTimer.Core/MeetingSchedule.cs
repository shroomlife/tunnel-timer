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
    private bool _hasUnsavedChanges;

    public MeetingSchedule(IMeetingStore store)
    {
        _store = store;
        _meetings = Sort(Sanitize(store.Load()));
    }

    /// <summary>Raised after the list changed, whether or not it could be saved.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the list could not be written. The change itself still applies.</summary>
    public event EventHandler<Exception>? SaveFailed;

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

    /// <summary>Writes the list again if an earlier save failed. Cheap when nothing is pending.</summary>
    public void RetryPendingSave()
    {
        if (_hasUnsavedChanges)
        {
            Save();
        }
    }

    private static bool IsStale(Meeting meeting, DateTimeOffset now) => now - meeting.StartsAt > StaleAfter;

    private static List<Meeting> Sort(IEnumerable<Meeting> meetings) =>
        [.. meetings.OrderBy(meeting => meeting.StartsAt)];

    /// <summary>
    /// Stored data is not trusted: a hand-edited or damaged file can hold empty entries or a
    /// link that is not a web address. Broken entries are dropped, a bad link is removed, so
    /// what reaches the rest of the app always satisfies the same rules as fresh input.
    /// </summary>
    private static IEnumerable<Meeting> Sanitize(IEnumerable<Meeting?>? stored)
    {
        foreach (var meeting in stored ?? [])
        {
            if (meeting is null || meeting.Id == Guid.Empty || meeting.Title is null)
            {
                continue;
            }

            yield return MeetingLink.IsAllowed(meeting.Link) ? meeting : meeting with { Link = null };
        }
    }

    private void Apply(IEnumerable<Meeting> meetings)
    {
        // The in-memory change and its notification must not depend on the disk: a widget
        // that cannot be dismissed because a file was locked would be worse than a list that
        // is saved a moment later.
        _meetings = Sort(meetings);
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        try
        {
            _store.Save(_meetings);
            _hasUnsavedChanges = false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _hasUnsavedChanges = true;
            SaveFailed?.Invoke(this, exception);
        }
    }
}
