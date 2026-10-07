using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class PinLayoutTests
{
    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(168)]
    [InlineData(192)]
    public void DisconnectedMonitorOffsetsAndSizesClampToAvailableWorkArea(int dpi)
    {
        var work = new PixelRect(-1920, -200, 1920, 1080);
        var placed = new PinLayout("missing", 9000, -9000, 320, 220, 0.5, false, true).Place(work, dpi);
        Assert.Equal(work.Y, placed.Y);
        Assert.Equal(work.Right, placed.Right);
        Assert.Equal(DpiGeometry.ToPixel(320, dpi), placed.Width);
        Assert.Equal(placed, placed.Intersect(work));
        var oversized = new PinLayout("", 0, 0, 4096, 4096, 1, true, false).Place(work, dpi);
        Assert.Equal(work, oversized);
    }
    [Fact]
    public void LayoutPersistsAcrossRestartAndWriteFailureRollsBack()
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-pin-layout-" + Guid.NewGuid().ToString("N"));
        try
        {
            var fail = false;
            var repository = new CaptureRepository(root, (path, data) => { if (fail) throw new IOException(); AtomicFile.Write(path, data); });
            var capture = repository.Add([1], 20, 30); repository.SetPinned(capture, true);
            var layout = new PinLayout("display", -10, 50, 400, 200, 0.35, false, true);
            repository.SetPinLayout(capture, layout);
            var restarted = new CaptureRepository(root); restarted.Load();
            Assert.Equal(layout, Assert.Single(restarted.Captures).PinLayout);
            fail = true;
            Assert.Throws<IOException>(() => repository.SetPinLayout(capture, layout with { Opacity = 0.7 }));
            Assert.Equal(layout, capture.PinLayout);
            Assert.Throws<InvalidOperationException>(() => repository.SetPinLayout(new CaptureRecord(), layout));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void NonfiniteMetadataNormalizesWithoutBreakingCaptureHistory()
    {
        var normalized = new PinLayout(null!, double.NaN, double.PositiveInfinity, -1, double.NaN, double.NaN, true, false).Normalize();
        Assert.Equal("", normalized.MonitorIdentity); Assert.Equal(0, normalized.X); Assert.Equal(0, normalized.Y);
        Assert.Equal(80, normalized.Width); Assert.Equal(220, normalized.Height); Assert.Equal(1, normalized.Opacity);
    }
}

