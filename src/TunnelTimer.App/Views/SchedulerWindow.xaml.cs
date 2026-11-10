using System.Globalization;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TunnelTimer.App.Services;
using TunnelTimer.App.ViewModels;
using Windows.Graphics;
using Windows.System;
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

        // Applied first and configured afterwards, like the widget: IsAlwaysOnTop set before
        // SetPresenter did not reach the window.
        var presenter = OverlappedPresenter.Create();
        AppWindow.SetPresenter(presenter);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;

        // The panel behaves like a notification area flyout: while it is open it stays above
        // other windows. Windows does not always let a background app take the foreground,
        // and a panel that opens behind the browser looks like a click that did nothing. On
        // top it is visible either way, and it stays in view while the user fetches a meeting
        // link from another window.
        presenter.IsAlwaysOnTop = true;

        // Adding a meeting is what the panel is opened for, so it closes once that is done.
        ViewModel.MeetingAdded += (_, _) => AppWindow.Hide();

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
        // async void: the caller is a click or an activation, so nothing can observe a task.
        // Every failure therefore has to end here.
        try
        {
            // Opening resets the form. A click while the panel is already open only brings it
            // to the front and keeps what the user has typed.
            var isOpening = !AppWindow.IsVisible;
            if (isOpening)
            {
                PlaceOnScreen();
                ViewModel.ResetForm();
            }

            // Activate alone left the panel behind other windows when it was already open or
            // when another app held the foreground. A click on the notification area icon or
            // a second launch gives this process the right to take the foreground, so it is
            // claimed explicitly.
            Activate();
            AppWindow.MoveInZOrderAtTop();
            PInvoke.SetForegroundWindow(new HWND(WindowNative.GetWindowHandle(this)));

            // The panel opens ready for typing: digits and Enter add a meeting.
            if (isOpening)
            {
                FocusTimeInput();
            }

            await ViewModel.RefreshStartupStatusAsync();
        }
        catch (Exception exception)
        {
            AppLog.Error("Showing the scheduler failed", exception);
        }
    }

    private void PlaceOnScreen()
    {
        var scale = PInvoke.GetDpiForWindow(new HWND(WindowNative.GetWindowHandle(this))) / DefaultDpi;
        var margin = (int)Math.Round(ScreenMarginDip * scale);
        var workArea = DisplayArea.Primary.WorkArea;

        // On a small or strongly scaled screen the panel is shortened to fit; its content scrolls.
        var size = new SizeInt32(
            Math.Min((int)Math.Round(WidthDip * scale), workArea.Width - 2 * margin),
            Math.Min((int)Math.Round(HeightDip * scale), workArea.Height - 2 * margin));

        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + workArea.Width - size.Width - margin,
            workArea.Y + workArea.Height - size.Height - margin,
            size.Width,
            size.Height));
    }

    private void OnStartTimeFlyoutOpening(object? sender, object e) => StartTimeFlyout.Time = ViewModel.SelectedTime;

    private void OnStartTimePicked(TimePickerFlyout sender, TimePickedEventArgs args) =>
        ViewModel.SelectedTime = args.NewTime;

    private void FocusTimeInput()
    {
        // The first time the panel opens its content is not loaded yet and cannot take the
        // focus; the first control in tab order would get it instead.
        if (TimeInputBox.IsLoaded)
        {
            TimeInputBox.Focus(FocusState.Programmatic);
            return;
        }

        TimeInputBox.Loaded += OnTimeInputLoaded;
    }

    private void OnTimeInputLoaded(object sender, RoutedEventArgs e)
    {
        TimeInputBox.Loaded -= OnTimeInputLoaded;
        TimeInputBox.Focus(FocusState.Programmatic);
    }

    private void OnTimeInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Enter adds a meeting at the typed time, with the title and link from the form.
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            ViewModel.AddFromTimeInput();
        }
    }

    private async void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        try
        {
            // Toggled also fires when the binding pushes the state in; only a real change counts.
            if (StartupToggle.IsOn != ViewModel.IsStartupEnabled)
            {
                await ViewModel.SetStartupEnabledAsync(StartupToggle.IsOn);

                // Windows may refuse the change; the switch then goes back to the real state.
                StartupToggle.IsOn = ViewModel.IsStartupEnabled;
            }
        }
        catch (Exception exception)
        {
            AppLog.Error("Changing start with Windows failed", exception);
        }
    }
}
