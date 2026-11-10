using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using TunnelTimer.App.Services;
using TunnelTimer.App.ViewModels;
using TunnelTimer.Core;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using WinRT.Interop;

namespace TunnelTimer.App.Views;

/// <summary>
/// The countdown widget: borderless, always on top, draggable anywhere except on its button,
/// and impossible to close or minimize.
/// </summary>
public sealed partial class WidgetWindow : Window
{
    // Sizes in device-independent pixels; scaled to the monitor's DPI before use.
    private const double WidthDip = 260;
    private const double HeightDip = 228;
    private const double HeightWithLinkDip = 264;
    private const double ScreenMarginDip = 24;
    private const double DefaultDpi = 96;

    private readonly WidgetView _view;
    private readonly WidgetPositionStore _positionStore;
    private readonly InputNonClientPointerSource _nonClientSource;

    // Two seconds at five passes a second: far beyond a first frame, short for a reminder.
    private const int MaxPassesWhileCloaked = 10;

    private bool _isCloaked;
    private int _passesWhileCloaked;

    public WidgetWindow(WidgetViewModel viewModel, WidgetPositionStore positionStore)
    {
        _positionStore = positionStore;
        InitializeComponent();

        _view = new WidgetView(viewModel);
        Content = _view;

        Title = viewModel.WindowTitle;

        // The presenter is applied first and configured afterwards: it is a live object, and
        // IsAlwaysOnTop set before SetPresenter did not reach the window.
        var presenter = OverlappedPresenter.Create();
        AppWindow.SetPresenter(presenter);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        presenter.IsAlwaysOnTop = true;

        AppWindow.IsShownInSwitchers = false;
        AppWindow.Closing += (_, args) => args.Cancel = true;

        _nonClientSource = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
        _nonClientSource.ExitedMoveSize += (_, _) => _positionStore.Save(AppWindow.Position);

        // Loaded covers the first layout, SizeChanged every later one including DPI changes.
        _view.Loaded += (_, _) => UpdateDragRegions();
        _view.SizeChanged += (_, _) => UpdateDragRegions();
    }

    public void Update(CountdownSnapshot snapshot)
    {
        _view.ViewModel.Update(snapshot);

        var shouldBeVisible = snapshot.Phase != CountdownPhase.Hidden;
        if (shouldBeVisible)
        {
            // The meeting on display can change while the widget is up, and with it the
            // number of buttons.
            FitToContent();
        }

        if (shouldBeVisible == AppWindow.IsVisible)
        {
            // Safety net for the cloak below: a countdown nobody can see is the worst failure,
            // so the widget is revealed after a few passes even if no frame was reported.
            if (_isCloaked && ++_passesWhileCloaked >= MaxPassesWhileCloaked)
            {
                AppLog.Error("The widget reported no rendered frame; revealed without one", new TimeoutException());
                Reveal();
            }

            return;
        }

        if (shouldBeVisible)
        {
            PlaceOnScreen();

            // The move can land on a monitor with another scaling. Fitting again here gives
            // the right size before the first frame, not a visible jump one pass later.
            FitToContent();
            _view.SetGripAnimation(isRunning: true);

            // Windows shows a window at once, XAML draws into it a moment later; in between
            // the widget was an empty white box for about a third of a second. It is therefore
            // shown cloaked, composed but not visible, and revealed with its first frame.
            Cloak();

            // Shown without activation: the widget must not pull focus out of the user's work.
            AppWindow.Show(activateWindow: false);
        }
        else
        {
            AppWindow.Hide();
            Reveal();
            _view.SetGripAnimation(isRunning: false);
        }
    }

    private void Cloak()
    {
        if (!SetCloak(isCloaked: true))
        {
            return;
        }

        _isCloaked = true;
        _passesWhileCloaked = 0;
        CompositionTarget.Rendered += OnFrameRendered;
    }

    private void Reveal()
    {
        if (!_isCloaked)
        {
            return;
        }

        CompositionTarget.Rendered -= OnFrameRendered;
        _isCloaked = false;
        SetCloak(isCloaked: false);
    }

    private async void OnFrameRendered(object? sender, RenderedEventArgs e)
    {
        // The event covers every window of the app. Only a frame drawn after the widget's
        // content is in the tree contains the widget.
        if (!_view.IsLoaded)
        {
            return;
        }

        CompositionTarget.Rendered -= OnFrameRendered;

        // async void: nothing observes this handler, so every failure ends here, and the
        // widget is revealed either way.
        try
        {
            // Rendered means XAML has drawn the frame, not that the compositor has taken it
            // over. Revealing right away still showed the bare window for two or three frames.
            await Compositor.RequestCommitAsync();
        }
        catch (Exception exception)
        {
            AppLog.Error("Waiting for the widget's first frame failed", exception);
        }

        Reveal();
    }

    private bool SetCloak(bool isCloaked)
    {
        BOOL value = isCloaked;
        var result = PInvoke.DwmSetWindowAttribute(
            new HWND(WindowNative.GetWindowHandle(this)),
            DWMWINDOWATTRIBUTE.DWMWA_CLOAK,
            MemoryMarshal.AsBytes(new ReadOnlySpan<BOOL>(in value)));
        if (result.Failed)
        {
            AppLog.Error("Cloaking the widget failed", Marshal.GetExceptionForHR(result.Value) ?? new InvalidOperationException());
        }

        return result.Succeeded;
    }

    private double GetScale() =>
        PInvoke.GetDpiForWindow(new HWND(WindowNative.GetWindowHandle(this))) / DefaultDpi;

    private SizeInt32 GetDesiredSize(double scale)
    {
        var height = _view.ViewModel.HasLink ? HeightWithLinkDip : HeightDip;
        return new SizeInt32((int)Math.Round(WidthDip * scale), (int)Math.Round(height * scale));
    }

    private void FitToContent()
    {
        var size = GetDesiredSize(GetScale());
        if (AppWindow.Size != size)
        {
            AppWindow.Resize(size);
        }
    }

    private void PlaceOnScreen()
    {
        var scale = GetScale();
        var size = GetDesiredSize(scale);

        // The remembered position is only used while the whole widget still fits on a
        // connected monitor. After a resolution, scaling or monitor change a partly visible
        // widget could no longer be grabbed, so it goes back to the default place instead.
        if (_positionStore.Load() is { } saved
            && DisplayArea.GetFromRect(new RectInt32(saved.X, saved.Y, size.Width, size.Height), DisplayAreaFallback.None) is { } area
            && Contains(area.WorkArea, saved, size))
        {
            AppWindow.Move(saved);
            return;
        }

        var workArea = DisplayArea.Primary.WorkArea;
        var margin = (int)Math.Round(ScreenMarginDip * scale);
        AppWindow.Move(new PointInt32(workArea.X + workArea.Width - size.Width - margin, workArea.Y + margin));
    }

    /// <summary>
    /// Declares everything above the button a caption region, so it drags the window like a
    /// title bar. The button row stays client area and keeps receiving clicks; a passthrough
    /// region inside a caption region was measured to still hit-test as caption.
    /// </summary>
    private void UpdateDragRegions()
    {
        if (_view.XamlRoot is not { } xamlRoot)
        {
            return;
        }

        var dragSurface = new Rect(0, 0, _view.ActualWidth, _view.GetInteractiveBounds().Top);
        _nonClientSource.SetRegionRects(
            NonClientRegionKind.Caption,
            [ToPixels(dragSurface, xamlRoot.RasterizationScale)]);
    }

    private static bool Contains(RectInt32 area, PointInt32 position, SizeInt32 size) =>
        position.X >= area.X
        && position.Y >= area.Y
        && position.X + size.Width <= area.X + area.Width
        && position.Y + size.Height <= area.Y + area.Height;

    private static RectInt32 ToPixels(Rect bounds, double scale) => new(
        (int)Math.Round(bounds.X * scale),
        (int)Math.Round(bounds.Y * scale),
        (int)Math.Round(bounds.Width * scale),
        (int)Math.Round(bounds.Height * scale));
}
