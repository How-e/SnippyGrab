using System.Text.Json;
using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class MonitorCaptureTests
{
    [Theory]
    [InlineData(-1920, -300, 1920, 1080)]
    [InlineData(3840, 0, 1080, 1920)]
    [InlineData(0, 0, 3840, 2160)]
    public void CapturesFullPhysicalBounds(int x, int y, int width, int height)
    {
        var bounds = new PixelRect(x, y, width, height);
        Assert.Equal(bounds, MonitorCapture.Resolve([("primary", new(0, 0, 1920, 1080)), ("chosen", bounds)], new("chosen")));
    }
    [Fact]
    public void DisconnectAmbiguityAndOversizeNeverFallback()
    {
        Assert.Throws<InvalidOperationException>(() => MonitorCapture.Resolve([("primary", new(0, 0, 1920, 1080))], new("missing")));
        Assert.Throws<InvalidOperationException>(() => MonitorCapture.Resolve([("same", new(0, 0, 1, 1)), ("same", new(1, 0, 1, 1))], new("same")));
        Assert.Throws<InvalidDataException>(() => MonitorCapture.Resolve([("large", new(0, 0, 10000, 8001))], new("large")));
    }
    [Fact]
    public void OldSettingsPreserveModeAndDisableNewShortcut()
    {
        var old = JsonSerializer.Deserialize<Settings>("{\"DefaultCaptureMode\":3,\"DockMonitorIdentity\":\"shelf\"}")!; old.Validate();
        Assert.Equal(CaptureMode.ActiveWindow, old.DefaultCaptureMode); Assert.Equal(new Hotkey(0, 0), old.MonitorHotkey);
        Assert.Equal(4, (int)CaptureMode.Monitor);
        old.MonitorHotkey = new(120, 6);
        var registered = HotkeyRegistration.Register(old, (_, _) => true, new List<string>());
        Assert.Equal(CaptureMode.Monitor, registered[6]); Assert.Equal("shelf", old.DockMonitorIdentity);
    }
}
