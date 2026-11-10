namespace TunnelTimer.Core;

/// <summary>
/// A meeting the user wants to be reminded of. <see cref="Link"/> is the optional address the
/// widget opens when the user joins.
/// </summary>
public sealed record Meeting(Guid Id, string Title, DateTimeOffset StartsAt, Uri? Link = null);
