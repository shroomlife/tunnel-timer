namespace TunnelTimer.Core.Tests;

public sealed class CountdownCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 11, 0, 0, TimeSpan.FromHours(1));

    private static Meeting MeetingIn(TimeSpan offset, string title = "Call") =>
        new(Guid.NewGuid(), title, Now + offset);

    [Fact]
    public void No_meetings_is_hidden()
    {
        var snapshot = CountdownCalculator.Calculate([], Now);

        Assert.Equal(CountdownSnapshot.Hidden, snapshot);
    }

    [Fact]
    public void Meeting_beyond_lead_time_is_hidden()
    {
        var snapshot = CountdownCalculator.Calculate([MeetingIn(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1))], Now);

        Assert.Equal(CountdownPhase.Hidden, snapshot.Phase);
        Assert.Null(snapshot.Meeting);
    }

    [Fact]
    public void Meeting_exactly_at_lead_time_is_upcoming()
    {
        var meeting = MeetingIn(TimeSpan.FromMinutes(10));

        var snapshot = CountdownCalculator.Calculate([meeting], Now);

        Assert.Equal(CountdownPhase.Upcoming, snapshot.Phase);
        Assert.Equal(meeting, snapshot.Meeting);
        Assert.Equal(TimeSpan.FromMinutes(10), snapshot.TimeUntilStart);
    }

    [Fact]
    public void Meeting_just_above_imminent_threshold_is_upcoming()
    {
        var snapshot = CountdownCalculator.Calculate([MeetingIn(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1))], Now);

        Assert.Equal(CountdownPhase.Upcoming, snapshot.Phase);
    }

    [Fact]
    public void Meeting_exactly_at_imminent_threshold_is_imminent()
    {
        var snapshot = CountdownCalculator.Calculate([MeetingIn(TimeSpan.FromMinutes(2))], Now);

        Assert.Equal(CountdownPhase.Imminent, snapshot.Phase);
    }

    [Fact]
    public void Meeting_starting_now_is_overdue()
    {
        var snapshot = CountdownCalculator.Calculate([MeetingIn(TimeSpan.Zero)], Now);

        Assert.Equal(CountdownPhase.Overdue, snapshot.Phase);
        Assert.Equal(TimeSpan.Zero, snapshot.TimeUntilStart);
    }

    [Fact]
    public void Started_meeting_is_overdue_with_negative_time()
    {
        var snapshot = CountdownCalculator.Calculate([MeetingIn(TimeSpan.FromSeconds(-83))], Now);

        Assert.Equal(CountdownPhase.Overdue, snapshot.Phase);
        Assert.Equal(TimeSpan.FromSeconds(-83), snapshot.TimeUntilStart);
    }

    [Fact]
    public void Earliest_meeting_wins_regardless_of_order()
    {
        var later = MeetingIn(TimeSpan.FromMinutes(8), "Later");
        var earlier = MeetingIn(TimeSpan.FromMinutes(3), "Earlier");

        var snapshot = CountdownCalculator.Calculate([later, earlier], Now);

        Assert.Equal(earlier, snapshot.Meeting);
    }

    [Fact]
    public void Overdue_meeting_stays_in_front_of_an_upcoming_one()
    {
        var overdue = MeetingIn(TimeSpan.FromMinutes(-5), "Overdue");
        var upcoming = MeetingIn(TimeSpan.FromMinutes(4), "Upcoming");

        var snapshot = CountdownCalculator.Calculate([upcoming, overdue], Now);

        Assert.Equal(overdue, snapshot.Meeting);
        Assert.Equal(CountdownPhase.Overdue, snapshot.Phase);
    }
}
