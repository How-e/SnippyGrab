using SnippyGrab.App.Services;
using SnippyGrab.Core;

namespace SnippyGrab.IntegrationTests;

public sealed class MonitorTopologyTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(3)]
    public void EmptyEnumerationUsesPositiveFallbackInsteadOfIndexingMissingMonitors(int configured)
    {
        var desktop = new PixelRect(-1920, 0, 3840, 1080);
        var fallback = MonitorService.Select([], desktop, default, configured, "disconnected-display");
        Assert.Equal(desktop, fallback.Bounds); Assert.Equal(desktop, fallback.Work); Assert.Equal(96u, fallback.Dpi); Assert.True(fallback.Primary);
        Assert.False(MonitorService.Select([], default, default, configured).Bounds.IsEmpty);
        MonitorInfo[] reconnected = [new(1, desktop, desktop, 144, 0, "disconnected-display", true)];
        Assert.Same(reconnected[0], MonitorService.Select(reconnected, desktop, default, configured, "disconnected-display"));
    }
}
