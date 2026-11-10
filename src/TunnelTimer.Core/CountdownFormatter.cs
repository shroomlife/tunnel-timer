using System.Globalization;

namespace TunnelTimer.Core;

public static class CountdownFormatter
{
    /// <summary>
    /// Formats the time until the start as <c>mm:ss</c>, or as <c>+mm:ss</c> once the meeting
    /// has started. Remaining time is rounded up so the display reaches 00:00 exactly at the
    /// start; elapsed time is rounded down so it leaves +00:00 after one full second.
    /// </summary>
    public static string Format(TimeSpan timeUntilStart)
    {
        var isOverdue = timeUntilStart <= TimeSpan.Zero;
        var totalSeconds = isOverdue
            ? (long)Math.Floor(-timeUntilStart.TotalSeconds)
            : (long)Math.Ceiling(timeUntilStart.TotalSeconds);

        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        var text = string.Create(CultureInfo.InvariantCulture, $"{minutes:00}:{seconds:00}");

        return isOverdue && totalSeconds > 0 ? "+" + text : text;
    }
}
