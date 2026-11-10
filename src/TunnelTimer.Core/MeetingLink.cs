namespace TunnelTimer.Core;

public static class MeetingLink
{
    /// <summary>
    /// Reads the optional link a user typed. Empty input is valid and yields no link. Only
    /// absolute http and https addresses are accepted, because the link is later handed to
    /// the system to open and must not be able to start an arbitrary protocol handler.
    /// </summary>
    public static bool TryParse(string? text, out Uri? link)
    {
        link = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var parsed) || !IsAllowed(parsed))
        {
            return false;
        }

        link = parsed;
        return true;
    }

    /// <summary>Whether a link may be stored and opened: no link at all, or a web address.</summary>
    public static bool IsAllowed(Uri? link) =>
        link is null
        || (link.IsAbsoluteUri && (link.Scheme == Uri.UriSchemeHttps || link.Scheme == Uri.UriSchemeHttp));
}
