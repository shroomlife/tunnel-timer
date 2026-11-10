using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using TunnelTimer.App.Services;
using TunnelTimer.App.ViewModels;
using TunnelTimer.App.Views;
using TunnelTimer.Core;
using Windows.Storage;

namespace TunnelTimer.App;

public partial class App : Application
{
    // Fields keep the long-lived objects alive for the lifetime of the process.
    private WidgetWindow? _widgetWindow;
    private SchedulerWindow? _schedulerWindow;
    private TrayIcon? _trayIcon;
    private CountdownTicker? _ticker;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // The app lives in the notification area: closing or hiding windows must not end it.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;

        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        var timeProvider = TimeProvider.System;
        var strings = new Strings();
        var schedule = new MeetingSchedule(new JsonMeetingStore(ApplicationData.Current.LocalFolder.Path));

        // Meeting links are web addresses and open in the default browser, which hands a
        // Zoom link over to the installed Zoom client.
        var widgetViewModel = new WidgetViewModel(
            schedule,
            strings,
            link => Windows.System.Launcher.LaunchUriAsync(link).AsTask());
        var widgetWindow = new WidgetWindow(widgetViewModel, new WidgetPositionStore());
        var schedulerWindow = new SchedulerWindow(
            new SchedulerViewModel(schedule, new StartupService(), strings, timeProvider, Quit),
            strings);

        _widgetWindow = widgetWindow;
        _schedulerWindow = schedulerWindow;
        _trayIcon = new TrayIcon(strings.Get("AppDisplayName"), schedulerWindow.ShowPanel);

        _ticker = new CountdownTicker(dispatcherQueue, schedule, timeProvider);
        _ticker.SnapshotUpdated += (_, snapshot) => widgetWindow.Update(snapshot);
        _ticker.Start();

        // A second launch is redirected here by Program.Main and opens the scheduler.
        var instance = AppInstance.GetCurrent();
        instance.Activated += (_, _) => dispatcherQueue.TryEnqueue(schedulerWindow.ShowPanel);

        // Started by Windows at sign-in or restarted after a crash: stay silent in the
        // notification area. Only a launch by the user opens the scheduler.
        var isStartedByWindows =
            instance.GetActivatedEventArgs().Kind == ExtendedActivationKind.StartupTask
            || Environment.GetCommandLineArgs().Contains(Program.RestartArgument);
        if (!isStartedByWindows)
        {
            schedulerWindow.ShowPanel();
        }
    }

    private void Quit()
    {
        _trayIcon?.Dispose();
        Exit();
    }
}
