using CommunityToolkit.Mvvm.Input;

namespace TunnelTimer.App.ViewModels;

/// <summary>One row in the scheduler's list of open meetings.</summary>
public sealed record MeetingItem(string Title, string StartText, bool HasLink, IRelayCommand RemoveCommand);
