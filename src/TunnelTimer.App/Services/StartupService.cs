using Windows.ApplicationModel;

namespace TunnelTimer.App.Services;

/// <summary>
/// Turns the "start with Windows" task from the package manifest on and off. Starting with
/// Windows is a convenience: when Windows refuses or fails, the switch is shown as
/// unavailable and the app carries on.
/// </summary>
public sealed class StartupService
{
    // Must match the TaskId of the uap5:StartupTask in Package.appxmanifest.
    private const string TaskId = "TunnelTimerStartup";

    private static readonly StartupStatus Unavailable = new(IsEnabled: false, CanChange: false);

    public async Task<StartupStatus> GetStatusAsync()
    {
        try
        {
            return ToStatus((await StartupTask.GetAsync(TaskId)).State);
        }
        catch (Exception exception)
        {
            AppLog.Error("Reading the startup task failed", exception);
            return Unavailable;
        }
    }

    public async Task<StartupStatus> SetEnabledAsync(bool isEnabled)
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            if (isEnabled)
            {
                return ToStatus(await task.RequestEnableAsync());
            }

            task.Disable();
            return ToStatus(task.State);
        }
        catch (Exception exception)
        {
            AppLog.Error("Changing the startup task failed", exception);
            return Unavailable;
        }
    }

    private static StartupStatus ToStatus(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled => new StartupStatus(IsEnabled: true, CanChange: true),
        StartupTaskState.Disabled => new StartupStatus(IsEnabled: false, CanChange: true),
        StartupTaskState.EnabledByPolicy => new StartupStatus(IsEnabled: true, CanChange: false),

        // DisabledByUser and DisabledByPolicy: Windows does not let the app turn it back on.
        _ => Unavailable,
    };
}

public readonly record struct StartupStatus(bool IsEnabled, bool CanChange);
