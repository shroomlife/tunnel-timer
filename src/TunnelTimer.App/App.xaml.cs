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

        // The app runs unattended all day. An exception in a click or timer handler is logged
        // and the app keeps running: the countdown is recomputed from the clock on the next
        // pass, so a failed pass does not leave it in a wrong state.
        UnhandledException += (_, e) =>
        {
            AppLog.Error("Unhandled UI exception", e.Exception);
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // The app lives in the notification area: closing or hiding windows must not end it.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;

        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        var timeProvider = TimeProvider.System;
        var strings = new Strings();

#if DEBUG
        // Development tool: render the listing images and leave. See ScreenshotExport.
        if (ScreenshotExport.TryGetFolder(out var screenshotFolder))
        {
            ExportScreenshots(screenshotFolder, strings);
            return;
        }
#endif

        var schedule = new MeetingSchedule(new JsonMeetingStore(ApplicationData.Current.LocalFolder.Path));
        schedule.SaveFailed += (_, exception) => AppLog.Error("Saving meetings failed, will retry", exception);

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

        // A second launch is redirected here by Program.Main and opens the scheduler. This
        // also is the way in when the notification area icon could not be created.
        var instance = AppInstance.GetCurrent();
        instance.Activated += (_, _) => dispatcherQueue.TryEnqueue(schedulerWindow.ShowPanel);

        _trayIcon = CreateTrayIcon(strings.Get("AppDisplayName"), schedulerWindow.ShowPanel);

        _ticker = new CountdownTicker(dispatcherQueue, schedule, timeProvider);
        _ticker.SnapshotUpdated += (_, snapshot) => widgetWindow.Update(snapshot);
        _ticker.Start();

        // Started by Windows at sign-in or restarted after a crash: stay silent in the
        // notification area. Only a launch by the user opens the scheduler.
        var isStartedByWindows =
            instance.GetActivatedEventArgs().Kind == ExtendedActivationKind.StartupTask
            || Environment.GetCommandLineArgs().Contains(Program.RestartArgument);
        if (!isStartedByWindows || _trayIcon is null)
        {
            schedulerWindow.ShowPanel();
        }
    }

    /// <summary>
    /// The countdown matters more than the icon: if the shell refuses the icon, the app still
    /// runs and the scheduler stays reachable by launching the app again.
    /// </summary>
    private static TrayIcon? CreateTrayIcon(string toolTip, Action onClick)
    {
        try
        {
            return new TrayIcon(toolTip, onClick);
        }
        catch (Exception exception)
        {
            AppLog.Error("Creating the notification area icon failed", exception);
            return null;
        }
    }

#if DEBUG
    private async void ExportScreenshots(string folder, Strings strings)
    {
        // async void: nothing observes this, so the failure is logged here and the process
        // ends either way.
        try
        {
            await ScreenshotExport.RunAsync(folder, strings);
        }
        catch (Exception exception)
        {
            AppLog.Error("Exporting screenshots failed", exception);
        }

        Exit();
    }
#endif

    private void Quit()
    {
        _trayIcon?.Dispose();
        Exit();
    }
}
