using System.Globalization;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TunnelTimer.App.Services;
using TunnelTimer.App.ViewModels;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using WinRT.Interop;

namespace TunnelTimer.App.Views;

/// <summary>
/// The panel behind the notification area icon: add meetings, see the open ones, and control
/// the app. Closing it only hides it.
/// </summary>
public sealed partial class SchedulerWindow : Window
{
    // Sizes in device-independent pixels; scaled to the monitor's DPI before use.
    private const double WidthDip = 400;
    private const double HeightDip = 760;
    private const double ScreenMarginDip = 12;
    private const double DefaultDpi = 96;

    public SchedulerWindow(SchedulerViewModel viewModel, Strings strings)
    {
        ViewModel = viewModel;
        InitializeComponent();

        Title = strings.Get("AppDisplayName");
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        // The branded header replaces the system title bar and drags the window.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(HeaderBar);

        // The time picker follows the display language by default; the rest of the app
        // formats times with the regional format, so the picker is aligned to that.
        var usesTwentyFourHours = CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('H');
        StartTimeFlyout.ClockIdentifier = usesTwentyFourHours ? "24HourClock" : "12HourClock";

        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        AppWindow.SetPresenter(presenter);

        AppWindow.Closing += (_, args) =>
        {
            args.Cancel = true;
            AppWindow.Hide();
        };
    }

    public SchedulerViewModel ViewModel { get; }

    // Instance method on purpose: x:Bind resolves function bindings on the window instance.
#pragma warning disable CA1822
    public Visibility VisibleWhenFalse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
#pragma warning restore CA1822

    /// <summary>Shows the panel above the notification area and brings it to the front.</summary>
    public async void ShowPanel()
    {
        if (!AppWindow.IsVisible)
        {
            PlaceOnScreen();
        }

        Activate();
        await ViewModel.PrepareAsync();
    }

    private void PlaceOnScreen()
    {
        var scale = PInvoke.GetDpiForWindow(new HWND(WindowNative.GetWindowHandle(this))) / DefaultDpi;
        var size = new SizeInt32((int)Math.Round(WidthDip * scale), (int)Math.Round(HeightDip * scale));
        var margin = (int)Math.Round(ScreenMarginDip * scale);
        var workArea = DisplayArea.Primary.WorkArea;

        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + workArea.Width - size.Width - margin,
            workArea.Y + workArea.Height - size.Height - margin,
            size.Width,
            size.Height));
    }

    private void OnStartTimeFlyoutOpening(object? sender, object e) => StartTimeFlyout.Time = ViewModel.SelectedTime;

    private void OnStartTimePicked(TimePickerFlyout sender, TimePickedEventArgs args) =>
        ViewModel.SelectedTime = args.NewTime;

    private async void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        // Toggled also fires when the binding pushes the state in; only a real change counts.
        if (StartupToggle.IsOn != ViewModel.IsStartupEnabled)
        {
            await ViewModel.SetStartupEnabledAsync(StartupToggle.IsOn);

            // Windows may refuse the change; the switch then goes back to the real state.
            StartupToggle.IsOn = ViewModel.IsStartupEnabled;
        }
    }
}
