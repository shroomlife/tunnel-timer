namespace TunnelTimer.Core;

/// <summary>Persists the open meetings so they survive an app restart.</summary>
public interface IMeetingStore
{
    IReadOnlyList<Meeting> Load();

    void Save(IReadOnlyList<Meeting> meetings);
}
