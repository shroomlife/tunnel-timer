using System.Text.Json;
using System.Text.Json.Serialization;
using TunnelTimer.Core;

namespace TunnelTimer.App.Services;

/// <summary>Stores the open meetings as a JSON file in the given folder.</summary>
public sealed class JsonMeetingStore(string folderPath) : IMeetingStore
{
    private readonly string _filePath = Path.Combine(folderPath, "meetings.json");

    public IReadOnlyList<Meeting> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            using var stream = File.OpenRead(_filePath);
            return JsonSerializer.Deserialize(stream, MeetingJsonContext.Default.ListMeeting) ?? [];
        }
        catch (JsonException)
        {
            // An unreadable file must not block every future start. It is kept next to the
            // new one for inspection instead of being overwritten.
            File.Move(_filePath, _filePath + ".corrupt", overwrite: true);
            return [];
        }
    }

    public void Save(IReadOnlyList<Meeting> meetings)
    {
        // Write to a temporary file first so a crash mid-write cannot truncate the store.
        var temporaryPath = _filePath + ".tmp";
        using (var stream = File.Create(temporaryPath))
        {
            JsonSerializer.Serialize(stream, [.. meetings], MeetingJsonContext.Default.ListMeeting);
        }

        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}

[JsonSerializable(typeof(List<Meeting>))]
internal sealed partial class MeetingJsonContext : JsonSerializerContext;
