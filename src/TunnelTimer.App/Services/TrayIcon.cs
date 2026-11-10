using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TunnelTimer.App.Services;

/// <summary>The notification area icon. Any click on it opens the scheduler.</summary>
public sealed partial class TrayIcon : IDisposable
{
    private readonly TaskbarIcon _taskbarIcon;

    public TrayIcon(string toolTip, Action onClick)
    {
        var clickCommand = new RelayCommand(onClick);

        _taskbarIcon = new TaskbarIcon
        {
            // Stable id so Windows remembers the icon's visibility across app updates.
            Id = new Guid("5B0C5F0E-7C55-4B5E-9F4D-2F1D8C1B6A37"),
            ToolTipText = toolTip,
            Icon = LoadIcon(),
            // By default a left click is held back for the double-click time to tell the two
            // apart. There is no double-click action here, and the delay costs twice: the
            // panel opens half a second late, and by then Windows no longer lets the app take
            // the foreground, so the panel opened behind other windows.
            NoLeftClickDelay = true,
            LeftClickCommand = clickCommand,
            RightClickCommand = clickCommand,
            MenuActivation = PopupActivationMode.None,
        };

        // The default enables efficiency mode, which pins the whole process to idle priority
        // and EcoQoS. The countdown must keep ticking on time, so that is turned off.
        _taskbarIcon.ForceCreate(enablesEfficiencyMode: false);
    }

    public void Dispose() => _taskbarIcon.Dispose();

    /// <summary>
    /// Loads the image that matches the notification area's icon size for the system DPI, so
    /// the shell does not have to scale the smallest image up on scaled displays.
    /// </summary>
    private static System.Drawing.Icon LoadIcon()
    {
        var size = PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXSMICON, PInvoke.GetDpiForSystem());
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        return new System.Drawing.Icon(stream, size, size);
    }
}
