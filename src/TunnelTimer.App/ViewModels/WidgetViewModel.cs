using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TunnelTimer.App.Services;
using TunnelTimer.Core;

namespace TunnelTimer.App.ViewModels;

/// <param name="openLink">Opens a meeting link with the system; false when nothing handled it.</param>
public sealed partial class WidgetViewModel(
    MeetingSchedule schedule,
    Strings strings,
    Func<Uri, Task<bool>> openLink) : ObservableObject
{
    private Meeting? _meeting;

    public string WindowTitle { get; } = strings.Get("AppDisplayName");

    [ObservableProperty]
    public partial CountdownPhase Phase { get; private set; }

    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string TimeText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string StartText { get; private set; } = string.Empty;

    /// <summary>With a link the widget offers "Join meeting", without one only "I'm in".</summary>
    [ObservableProperty]
    public partial bool HasLink { get; private set; }

    public void Update(CountdownSnapshot snapshot)
    {
        _meeting = snapshot.Meeting;
        Phase = snapshot.Phase;

        if (snapshot.Meeting is not { } meeting)
        {
            return;
        }

        var startTime = meeting.StartsAt.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
        Title = meeting.Title;
        TimeText = CountdownFormatter.Format(snapshot.TimeUntilStart);
        StartText = string.Format(CultureInfo.CurrentCulture, strings.Get("WidgetStartsAtFormat"), startTime);
        HasLink = meeting.Link is not null;
    }

    /// <summary>The user is in the meeting: the entry is done and the widget disappears.</summary>
    [RelayCommand]
    private void Acknowledge()
    {
        if (_meeting is { } meeting)
        {
            schedule.Remove(meeting.Id);
        }
    }

    /// <summary>
    /// Opens the meeting link and then treats the meeting as joined. If the system could not
    /// open the link, the widget stays so the user is not left without a reminder.
    /// </summary>
    [RelayCommand]
    private async Task JoinAsync()
    {
        if (_meeting is { Link: { } link } meeting && await openLink(link))
        {
            schedule.Remove(meeting.Id);
        }
    }
}
