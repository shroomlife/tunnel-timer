using Windows.Graphics;
using Windows.Storage;

namespace TunnelTimer.App.Services;

/// <summary>Remembers where the user dragged the widget, in screen pixels.</summary>
public sealed class WidgetPositionStore
{
    private const string XKey = "WidgetPositionX";
    private const string YKey = "WidgetPositionY";

    private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

    public PointInt32? Load() =>
        _settings.Values[XKey] is int x && _settings.Values[YKey] is int y ? new PointInt32(x, y) : null;

    public void Save(PointInt32 position)
    {
        _settings.Values[XKey] = position.X;
        _settings.Values[YKey] = position.Y;
    }
}
