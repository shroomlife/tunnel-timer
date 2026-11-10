using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using TunnelTimer.App.Services;
using Windows.Win32;
using Windows.Win32.System.Recovery;

namespace TunnelTimer.App;

public static class Program
{
    /// <summary>Command line Windows passes when it restarts the app after a crash.</summary>
    public const string RestartArgument = "--restarted";

    private const string InstanceKey = "TunnelTimer";

    [STAThread]
    public static async Task<int> Main()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLog.Error("Unhandled exception, process ends", e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        };

        WinRT.ComWrappersSupport.InitializeComWrappers();

        // Single instance: a second launch hands its activation to the running instance,
        // which then opens the scheduler, and exits before any XAML is created.
        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!mainInstance.IsCurrent)
        {
            // This process was started by the user and may take the foreground; the running
            // instance may not. Passing the right on lets its panel come to the front.
            PInvoke.AllowSetForegroundWindow(mainInstance.ProcessId);
            await mainInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            return 0;
        }

        // Windows restarts the app if it crashes or hangs after running for at least 60 seconds.
        // A visible WinUI window can be terminated from outside by a UI Automation client
        // (microsoft/microsoft-ui-xaml#11139, no app-side fix), and a countdown that silently
        // disappears defeats its purpose. Open meetings are persisted, so the widget is back
        // within seconds. Not after a reboot: starting at sign-in is the user's own setting.
        // The registration is a safety net, so its failure is logged and the app starts anyway.
        var registration = PInvoke.RegisterApplicationRestart(RestartArgument, REGISTER_APPLICATION_RESTART_FLAGS.RESTART_NO_REBOOT);
        if (registration.Failed)
        {
            AppLog.Error("Registering for restart failed", registration);
        }

        Application.Start(static _ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            var app = new App();
            GC.KeepAlive(app);
        });

        return 0;
    }
}
