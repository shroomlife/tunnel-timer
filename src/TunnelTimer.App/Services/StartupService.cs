using Windows.ApplicationModel;

namespace TunnelTimer.App.Services;

/// <summary>Turns the "start with Windows" task from the package manifest on and off.</summary>
public sealed class StartupService
{
    // Must match the TaskId of the uap5:StartupTask in Package.appxmanifest.
    private const string TaskId = "TunnelTimerStartup";

    public async Task<StartupStatus> GetStatusAsync() => ToStatus((await StartupTask.GetAsync(TaskId)).State);

    public async Task<StartupStatus> SetEnabledAsync(bool isEnabled)
    {
        var task = await StartupTask.GetAsync(TaskId);
        if (isEnabled)
        {
            return ToStatus(await task.RequestEnableAsync());
        }

        task.Disable();
        return ToStatus(task.State);
    }

    private static StartupStatus ToStatus(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled => new StartupStatus(IsEnabled: true, CanChange: true),
        StartupTaskState.Disabled => new StartupStatus(IsEnabled: false, CanChange: true),
        StartupTaskState.EnabledByPolicy => new StartupStatus(IsEnabled: true, CanChange: false),

        // DisabledByUser and DisabledByPolicy: Windows does not let the app turn it back on.
        _ => new StartupStatus(IsEnabled: false, CanChange: false),
    };
}

public readonly record struct StartupStatus(bool IsEnabled, bool CanChange);
