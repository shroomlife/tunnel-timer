using Windows.Storage;

namespace TunnelTimer.App.Services;

/// <summary>
/// Appends errors to a small log file in the app data folder. Logging must never be the
/// reason the app fails, so every failure in here is swallowed on purpose.
/// </summary>
public static class AppLog
{
    private const long MaxBytes = 256 * 1024;
    private const string FileName = "app.log";

    private static readonly Lock Gate = new();

    public static void Error(string context, object? detail)
    {
        try
        {
            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, FileName);
            lock (Gate)
            {
                // Start over instead of growing without bound when something fails repeatedly.
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                {
                    File.Delete(path);
                }

                File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] {context}: {detail}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // Nothing sensible is left to do when the log itself cannot be written.
        }
    }
}
