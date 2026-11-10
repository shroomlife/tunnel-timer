using System.Text.Json;
using System.Text.Json.Serialization;
using TunnelTimer.Core;

namespace TunnelTimer.App.Services;

/// <summary>Stores the open meetings as a JSON file in the given folder.</summary>
public sealed class JsonMeetingStore(string folderPath) : IMeetingStore
{
    private readonly string _filePath = Path.Combine(folderPath, "meetings.json");

    /// <summary>
    /// Never throws: the app starts through this call, and a file it cannot read must not
    /// keep it from starting on every launch. An unreadable file is set aside for inspection.
    /// Entries are validated by <see cref="MeetingSchedule"/>, not here.
    /// </summary>
    public IReadOnlyList<Meeting> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            using var stream = File.OpenRead(_filePath);
            return JsonSerializer.Deserialize(stream, MeetingJsonContext.Default.ListMeeting) ?? [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            AppLog.Error("Loading meetings failed", exception);
            SetAside();
            return [];
        }
    }

    public void Save(IReadOnlyList<Meeting> meetings)
    {
        // Write to a temporary file first so a crash mid-write cannot truncate the store, and
        // flush it to the disk before the rename so a power loss cannot leave an empty file
        // under the real name.
        var temporaryPath = _filePath + ".tmp";
        using (var stream = File.Create(temporaryPath))
        {
            JsonSerializer.Serialize(stream, [.. meetings], MeetingJsonContext.Default.ListMeeting);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporaryPath, _filePath, overwrite: true);
    }

    private void SetAside()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Move(_filePath, _filePath + ".corrupt", overwrite: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Setting the unreadable meetings file aside failed", exception);
        }
    }
}

[JsonSerializable(typeof(List<Meeting>))]
internal sealed partial class MeetingJsonContext : JsonSerializerContext;
