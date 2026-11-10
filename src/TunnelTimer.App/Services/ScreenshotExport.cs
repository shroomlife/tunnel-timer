#if DEBUG
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TunnelTimer.App.ViewModels;
using TunnelTimer.App.Views;
using TunnelTimer.Core;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;

namespace TunnelTimer.App.Services;

/// <summary>
/// Development tool, not part of release builds: renders the real widget and panel in fixed
/// states to PNG files, three times the size they have on a 100 % display. XAML draws them at
/// that size itself, so the images are sharp without changing the Windows display scaling,
/// and they come out the same on every run. Started with
/// <c>TunnelTimer.exe --export-screenshots &lt;folder&gt;</c>; see scripts/render-store-assets.ps1.
/// </summary>
internal static class ScreenshotExport
{
    public const string Argument = "--export-screenshots";

    /// <summary>The factor docs/store/src/scene.html divides the image sizes by.</summary>
    private const double Scale = 3;

    /// <summary>
    /// What the widget's backdrop looks like on screen while the widget is not the active
    /// window, measured from a capture. The backdrop itself is drawn by Windows behind the
    /// XAML content and is not part of a rendered bitmap.
    /// </summary>
    private static readonly Color WidgetBackground = Color.FromArgb(255, 249, 249, 249);

    public static bool TryGetFolder(out string folder)
    {
        var arguments = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(arguments, Argument);
        folder = index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : string.Empty;
        return folder.Length > 0;
    }

    public static async Task RunAsync(string folder, Strings strings)
    {
        Directory.CreateDirectory(folder);

        // One fixed afternoon, so every run shows the same times.
        var today = DateTime.Today;
        DateTimeOffset At(int hour, int minute, int second = 0)
        {
            var local = today.AddHours(hour).AddMinutes(minute).AddSeconds(second);
            return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
        }

        var link = new Uri("https://zoom.us/j/1234567890");
        var weeklySync = new Meeting(Guid.NewGuid(), "Weekly sync", At(13, 59));

        await RenderWidgetAsync(folder, "widget-upcoming.png", strings, weeklySync, At(13, 50, 37));
        await RenderWidgetAsync(folder, "widget-imminent.png", strings, new Meeting(Guid.NewGuid(), "Client call", At(13, 52), link), At(13, 50, 27));
        await RenderWidgetAsync(folder, "widget-overdue.png", strings, new Meeting(Guid.NewGuid(), "Client call", At(13, 50), link), At(13, 51, 23));

        var schedule = new MeetingSchedule(new FixedMeetingStore([weeklySync, new Meeting(Guid.NewGuid(), "Client call", At(15, 30), link)]));
        var panel = new SchedulerWindow(
            new SchedulerViewModel(schedule, new StartupService(), strings, new FixedTimeProvider(At(13, 50, 37)), () => { }),
            strings);
        panel.ShowPanel();
        await RenderAsync(panel, Colors.Transparent, Path.Combine(folder, "panel.png"));
    }

    private static async Task RenderWidgetAsync(string folder, string fileName, Strings strings, Meeting meeting, DateTimeOffset now)
    {
        // A window per state: each gets its size before it is first shown.
        var schedule = new MeetingSchedule(new FixedMeetingStore([meeting]));
        var window = new WidgetWindow(new WidgetViewModel(schedule, strings, _ => Task.FromResult(false)), new WidgetPositionStore());
        window.Update(CountdownCalculator.Calculate(schedule.Meetings, now));
        // The same grip in every picture, not whatever the animation shows at that moment.
        ((WidgetView)window.Content).SetGripAnimation(isRunning: false);
        await RenderAsync(window, WidgetBackground, Path.Combine(folder, fileName));
        window.AppWindow.Hide();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static async Task RenderAsync(Window window, Color background, string path)
    {
        var content = (FrameworkElement)window.Content;
        if (!content.IsLoaded)
        {
            var loaded = new TaskCompletionSource();
            content.Loaded += (_, _) => loaded.TrySetResult();
            await loaded.Task;
        }

        content.UpdateLayout();
        var width = content.ActualWidth;
        var height = content.ActualHeight;

        // RenderTargetBitmap only stretches a finished 100 % image when asked for a larger
        // one. A scale transform on the live content makes XAML draw text and shapes at the
        // larger size itself; the bitmap is then taken one to one.
        // The bitmap covers what is drawn, not the element's size: without a background the
        // widget's padding was cut off, and decoration reaching past the window edge made
        // the panel image larger. So the frame is filled and the clip sits one level below it.
        var frame = new Grid
        {
            Width = width * Scale,
            Height = height * Scale,
            Background = new SolidColorBrush(background),
        };
        var clipper = new Canvas
        {
            Clip = new RectangleGeometry { Rect = new Rect(0, 0, width * Scale, height * Scale) },
        };
        window.Content = null;
        content.Width = width;
        content.Height = height;
        content.RenderTransform = new ScaleTransform { ScaleX = Scale, ScaleY = Scale };
        clipper.Children.Add(content);
        frame.Children.Add(clipper);
        window.Content = frame;
        window.AppWindow.ResizeClient(new SizeInt32((int)Math.Round(width * Scale), (int)Math.Round(height * Scale)));

        // Layout, image decoding and list items settle over the next frames. There is no
        // event for "nothing left to draw", so this tool simply waits.
        await Task.Delay(TimeSpan.FromSeconds(1));
        foreach (var element in Descendants(frame))
        {
            switch (element)
            {
                // Bitmaps are picked for the display scaling, which stays at 100 %. The
                // largest logo is set by hand so it is not a small one stretched.
                case Image { Source: BitmapImage } image:
                    image.Source = new BitmapImage(new Uri("ms-appx:///Assets/Logo.scale-400.png"));
                    break;

                // No text cursor in the picture: the focus goes to a button, without a focus ring.
                case RepeatButton button when FocusManager.GetFocusedElement(frame.XamlRoot) is TextBox:
                    button.Focus(FocusState.Pointer);
                    break;
            }
        }

        await Task.Delay(TimeSpan.FromSeconds(1));

        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(frame);
        var pixels = (await bitmap.GetPixelsAsync()).ToArray();

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth,
            (uint)bitmap.PixelHeight,
            96,
            96,
            pixels);
        await encoder.FlushAsync();

        stream.Seek(0);
        await using var file = File.Create(path);
        await stream.AsStreamForRead().CopyToAsync(file);
    }

    private sealed class FixedMeetingStore(IReadOnlyList<Meeting> meetings) : IMeetingStore
    {
        public IReadOnlyList<Meeting> Load() => meetings;

        public void Save(IReadOnlyList<Meeting> meetings)
        {
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }
}
#endif
