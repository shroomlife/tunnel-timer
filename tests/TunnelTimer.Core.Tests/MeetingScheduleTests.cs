namespace TunnelTimer.Core.Tests;

public sealed class MeetingScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 11, 0, 0, TimeSpan.FromHours(1));

    private sealed class InMemoryMeetingStore(params Meeting[] initial) : IMeetingStore
    {
        public IReadOnlyList<Meeting> Saved { get; private set; } = initial;

        public int SaveCount { get; private set; }

        public IReadOnlyList<Meeting> Load() => Saved;

        public void Save(IReadOnlyList<Meeting> meetings)
        {
            Saved = [.. meetings];
            SaveCount++;
        }
    }

    [Fact]
    public void Loads_stored_meetings_ordered_by_start()
    {
        var later = new Meeting(Guid.NewGuid(), "Later", Now.AddHours(2));
        var earlier = new Meeting(Guid.NewGuid(), "Earlier", Now.AddHours(1));

        var schedule = new MeetingSchedule(new InMemoryMeetingStore(later, earlier));

        Assert.Equal([earlier, later], schedule.Meetings);
    }

    [Fact]
    public void Add_stores_trimmed_meeting_in_order_and_raises_changed()
    {
        var store = new InMemoryMeetingStore();
        var schedule = new MeetingSchedule(store);
        var changedCount = 0;
        schedule.Changed += (_, _) => changedCount++;

        var later = schedule.Add("  Later ", Now.AddMinutes(30), Now);
        var earlier = schedule.Add("Earlier", Now.AddMinutes(15), Now);

        Assert.NotNull(later);
        Assert.NotNull(earlier);
        Assert.Equal("Later", later.Title);
        Assert.Equal([earlier, later], schedule.Meetings);
        Assert.Equal([earlier, later], store.Saved);
        Assert.Equal(2, changedCount);
    }

    [Fact]
    public void Add_keeps_the_optional_link()
    {
        var store = new InMemoryMeetingStore();
        var schedule = new MeetingSchedule(store);
        var link = new Uri("https://us02web.zoom.us/j/1234567890");

        var withLink = schedule.Add("Call", Now.AddMinutes(30), Now, link);
        var withoutLink = schedule.Add("Sync", Now.AddMinutes(60), Now);

        Assert.Equal(link, withLink?.Link);
        Assert.Null(withoutLink?.Link);
        Assert.Equal(link, store.Saved[0].Link);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Add_rejects_start_that_is_not_in_the_future(int minutesFromNow)
    {
        var store = new InMemoryMeetingStore();
        var schedule = new MeetingSchedule(store);

        var meeting = schedule.Add("Call", Now.AddMinutes(minutesFromNow), Now);

        Assert.Null(meeting);
        Assert.Empty(schedule.Meetings);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void Remove_deletes_meeting_and_persists()
    {
        var keep = new Meeting(Guid.NewGuid(), "Keep", Now.AddHours(1));
        var drop = new Meeting(Guid.NewGuid(), "Drop", Now.AddHours(2));
        var store = new InMemoryMeetingStore(keep, drop);
        var schedule = new MeetingSchedule(store);

        schedule.Remove(drop.Id);

        Assert.Equal([keep], schedule.Meetings);
        Assert.Equal([keep], store.Saved);
    }

    [Fact]
    public void Remove_of_unknown_meeting_changes_nothing()
    {
        var store = new InMemoryMeetingStore(new Meeting(Guid.NewGuid(), "Keep", Now.AddHours(1)));
        var schedule = new MeetingSchedule(store);
        var changedCount = 0;
        schedule.Changed += (_, _) => changedCount++;

        schedule.Remove(Guid.NewGuid());

        Assert.Single(schedule.Meetings);
        Assert.Equal(0, store.SaveCount);
        Assert.Equal(0, changedCount);
    }

    [Fact]
    public void RemoveStale_drops_only_meetings_past_the_stale_limit()
    {
        var stale = new Meeting(Guid.NewGuid(), "Stale", Now - MeetingSchedule.StaleAfter - TimeSpan.FromSeconds(1));
        var atLimit = new Meeting(Guid.NewGuid(), "At limit", Now - MeetingSchedule.StaleAfter);
        var upcoming = new Meeting(Guid.NewGuid(), "Upcoming", Now.AddMinutes(5));
        var store = new InMemoryMeetingStore(stale, atLimit, upcoming);
        var schedule = new MeetingSchedule(store);

        schedule.RemoveStale(Now);

        Assert.Equal([atLimit, upcoming], schedule.Meetings);
        Assert.Equal(1, store.SaveCount);
    }
}
