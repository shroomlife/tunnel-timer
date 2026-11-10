using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TunnelTimer.App.Services;
using TunnelTimer.Core;

namespace TunnelTimer.App.ViewModels;

public sealed partial class SchedulerViewModel : ObservableObject
{
    private const int TimeStepMinutes = 5;
    private const int MinutesPerDay = 24 * 60;

    private readonly MeetingSchedule _schedule;
    private readonly StartupService _startupService;
    private readonly Strings _strings;
    private readonly TimeProvider _timeProvider;
    private readonly Action _quit;

    public SchedulerViewModel(
        MeetingSchedule schedule,
        StartupService startupService,
        Strings strings,
        TimeProvider timeProvider,
        Action quit)
    {
        _schedule = schedule;
        _startupService = startupService;
        _strings = strings;
        _timeProvider = timeProvider;
        _quit = quit;

        _schedule.Changed += (_, _) => RefreshMeetings();
        RefreshMeetings();
    }

    public ObservableCollection<MeetingItem> Meetings { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    /// <summary>Optional web address of the meeting, as typed.</summary>
    [ObservableProperty]
    public partial string Link { get; set; } = string.Empty;

    /// <summary>Start time of day for the new meeting, today.</summary>
    [ObservableProperty]
    public partial TimeSpan SelectedTime { get; set; }

    [ObservableProperty]
    public partial string SelectedTimeText { get; private set; } = string.Empty;

    /// <summary>How far away the selected time is and when the countdown will show.</summary>
    [ObservableProperty]
    public partial string SelectedTimeHint { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ErrorMessage { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasError { get; private set; }

    [ObservableProperty]
    public partial bool HasMeetings { get; private set; }

    [ObservableProperty]
    public partial bool IsStartupEnabled { get; private set; }

    [ObservableProperty]
    public partial bool CanChangeStartup { get; private set; }

    /// <summary>Resets the form. Called when the scheduler opens, not while it is in use.</summary>
    public void ResetForm()
    {
        Title = string.Empty;
        Link = string.Empty;
        HasError = false;
        SelectedTime = NextHalfHour(_timeProvider.GetLocalNow().TimeOfDay);
        DescribeSelectedTime();
    }

    public async Task RefreshStartupStatusAsync() => ApplyStartupStatus(await _startupService.GetStatusAsync());

    public async Task SetStartupEnabledAsync(bool isEnabled) =>
        ApplyStartupStatus(await _startupService.SetEnabledAsync(isEnabled));

    partial void OnSelectedTimeChanged(TimeSpan value) => DescribeSelectedTime();

    [RelayCommand]
    private void Add()
    {
        var now = _timeProvider.GetLocalNow();
        AddMeeting(ToNextStart(SelectedTime, now), now);
    }

    [RelayCommand]
    private void AddIn(string minutesFromNow)
    {
        var now = _timeProvider.GetLocalNow();
        AddMeeting(now.AddMinutes(int.Parse(minutesFromNow, CultureInfo.InvariantCulture)), now);
    }

    /// <summary>Moves the selected time one step earlier or later, on the five minute grid.</summary>
    [RelayCommand]
    private void StepTime(string direction)
    {
        var step = int.Parse(direction, CultureInfo.InvariantCulture) * TimeStepMinutes;
        var onGrid = (int)Math.Round(SelectedTime.TotalMinutes / TimeStepMinutes) * TimeStepMinutes;
        var wrapped = ((onGrid + step) % MinutesPerDay + MinutesPerDay) % MinutesPerDay;
        SelectedTime = TimeSpan.FromMinutes(wrapped);
    }

    [RelayCommand]
    private void Quit() => _quit();

    private static TimeSpan NextHalfHour(TimeSpan timeOfDay)
    {
        var halfHours = (long)Math.Floor(timeOfDay.TotalMinutes / 30) + 1;
        return TimeSpan.FromMinutes(halfHours * 30 % MinutesPerDay);
    }

    /// <summary>
    /// The next moment the clock shows this time of day: today, or tomorrow when that time
    /// has already passed. That keeps a meeting just after midnight possible to enter late in
    /// the evening; the hint under the time says which day is meant.
    /// </summary>
    private static DateTimeOffset ToNextStart(TimeSpan timeOfDay, DateTimeOffset now)
    {
        // .NET caches the local time zone. Clearing it here picks up a zone change that
        // happened while the app was running, for example after travelling.
        TimeZoneInfo.ClearCachedData();
        var zone = TimeZoneInfo.Local;

        var today = now.Date + timeOfDay;
        var startsToday = new DateTimeOffset(today, zone.GetUtcOffset(today));
        if (startsToday > now)
        {
            return startsToday;
        }

        var tomorrow = today.AddDays(1);
        return new DateTimeOffset(tomorrow, zone.GetUtcOffset(tomorrow));
    }

    private void DescribeSelectedTime()
    {
        var now = _timeProvider.GetLocalNow();
        var startsAt = ToNextStart(SelectedTime, now);
        SelectedTimeText = (now.Date + SelectedTime).ToString("t", CultureInfo.CurrentCulture);

        var timeUntilStart = startsAt - now;
        var totalMinutes = (int)Math.Ceiling(timeUntilStart.TotalMinutes);
        var duration = totalMinutes >= 60
            ? string.Create(CultureInfo.CurrentCulture, $"{totalMinutes / 60} h {totalMinutes % 60} min")
            : string.Create(CultureInfo.CurrentCulture, $"{totalMinutes} min");

        if (startsAt.LocalDateTime.Date > now.Date)
        {
            SelectedTimeHint = string.Format(CultureInfo.CurrentCulture, _strings.Get("SchedulerHintTomorrowFormat"), duration);
            return;
        }

        if (timeUntilStart <= CountdownCalculator.LeadTime)
        {
            SelectedTimeHint = string.Format(CultureInfo.CurrentCulture, _strings.Get("SchedulerHintSoonFormat"), duration);
            return;
        }

        var countdownStart = (startsAt - CountdownCalculator.LeadTime).ToString("t", CultureInfo.CurrentCulture);
        SelectedTimeHint = string.Format(CultureInfo.CurrentCulture, _strings.Get("SchedulerHintFormat"), duration, countdownStart);
    }

    private void AddMeeting(DateTimeOffset startsAt, DateTimeOffset now)
    {
        if (!MeetingLink.TryParse(Link, out var link))
        {
            ShowError("SchedulerErrorInvalidLink");
            return;
        }

        var title = string.IsNullOrWhiteSpace(Title) ? _strings.Get("DefaultMeetingTitle") : Title;
        if (_schedule.Add(title, startsAt, now, link) is null)
        {
            ShowError("SchedulerErrorPastTime");
            return;
        }

        Title = string.Empty;
        Link = string.Empty;
        HasError = false;
    }

    private void ShowError(string resourceKey)
    {
        ErrorMessage = _strings.Get(resourceKey);
        HasError = true;
    }

    private void ApplyStartupStatus(StartupStatus status)
    {
        IsStartupEnabled = status.IsEnabled;
        CanChangeStartup = status.CanChange;
    }

    private void RefreshMeetings()
    {
        Meetings.Clear();
        foreach (var meeting in _schedule.Meetings)
        {
            var startText = meeting.StartsAt.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
            Meetings.Add(new MeetingItem(
                meeting.Title,
                startText,
                meeting.Link is not null,
                new RelayCommand(() => _schedule.Remove(meeting.Id))));
        }

        HasMeetings = Meetings.Count > 0;
    }
}
