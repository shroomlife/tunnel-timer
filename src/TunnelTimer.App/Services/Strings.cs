using Microsoft.Windows.ApplicationModel.Resources;

namespace TunnelTimer.App.Services;

/// <summary>Localized strings for code; XAML uses x:Uid against the same resource files.</summary>
public sealed class Strings
{
    private readonly ResourceLoader _loader = new();

    public string Get(string key) => _loader.GetString(key);
}
