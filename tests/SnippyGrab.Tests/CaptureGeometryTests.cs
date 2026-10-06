using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class CaptureGeometryTests
{
    [Fact]
    public void SavedMonitorSurvivesEnumerationChangesAndReconnect()
    {
        Assert.Equal(1, MonitorIdentity.ConfiguredIndex(["A", "B"], 1, "B", 0));
        Assert.Equal(0, MonitorIdentity.ConfiguredIndex(["B", "A"], 1, "B", 1));
        Assert.Equal(1, MonitorIdentity.ConfiguredIndex(["C", "A"], 1, "B", 1));
        Assert.Equal(2, MonitorIdentity.ConfiguredIndex(["C", "A", "B"], 1, "B", 1));
        Assert.Equal(-1, MonitorIdentity.ConfiguredIndex(["A", "B"], -1, "B", 0));
    }
    [Theory]
    [InlineData(-1500, -400, 1400, 700)]
    [InlineData(1400, 700, -1500, -400)]
    public void CrossMonitorAndReversedRegionsUsePhysicalBounds(int x, int y, int endX, int endY)
    {
        var desktop = new PixelRect(-1920, -540, 4480, 1980);
        Assert.Equal(new PixelRect(-1500, -400, 2900, 1100), CaptureSelection.Region(x, y, endX, endY, desktop));
        Assert.Equal(new PixelRect(420, 140, 2900, 1100), CaptureSelection.Region(x, y, endX, endY, desktop)!.Value.RelativeTo(desktop));
    }
    [Fact]
    public void TinyAndOutsideRegionsCancelAndPartlyOffscreenRegionsClip()
    {
        var desktop = new PixelRect(-100, -100, 200, 200);
        Assert.Null(CaptureSelection.Region(0, 0, 0, 20, desktop));
        Assert.Null(CaptureSelection.Region(0, 0, 1, 20, desktop));
        Assert.Null(CaptureSelection.Region(200, 200, 400, 400, desktop));
        Assert.Equal(new PixelRect(-100, -100, 150, 150), CaptureSelection.Region(-200, -200, 50, 50, desktop));
    }
    [Theory]
    [InlineData(96)] [InlineData(120)] [InlineData(144)] [InlineData(168)] [InlineData(192)]
    public void DpiPlacementAndCursorHotspotStayInPhysicalPixels(int dpi)
    {
        Assert.Equal(237, DpiGeometry.ToPixel(DpiGeometry.ToDip(237, dpi), dpi));
        var work = new PixelRect(-2560, -1440, 2560, 1400);
        foreach (var position in Enum.GetValues<DockCorner>())
        {
            var placed = DockLayout.Place(work, DpiGeometry.ToPixel(224, dpi), DpiGeometry.ToPixel(146, dpi), position);
            Assert.Equal(placed, placed.Intersect(work));
        }
        Assert.Equal((239, 146), CaptureSelection.CursorOrigin(-2300, -1280, 21, 14, work));
    }
    [Fact]
    public void EdgePlacementCentersAndExpandsInwardWithoutChangingCornerDefaults()
    {
        var work = new PixelRect(-1000, -200, 1000, 800);
        foreach (var position in new[] { DockCorner.Top, DockCorner.Bottom, DockCorner.Left, DockCorner.Right })
        {
            var vertical = position is DockCorner.Top or DockCorner.Bottom;
            Assert.Equal(vertical ? DockOrientation.Vertical : DockOrientation.Horizontal, DockLayout.Orientation(DockOrientation.Vertical, position));
            var a = DockLayout.Place(work, 200, 120, position);
            var b = DockLayout.Place(work, vertical ? 200 : 600, vertical ? 360 : 120, position);
            if (vertical) { Assert.Equal(work.X + work.Width / 2, a.X + a.Width / 2); Assert.Equal(position == DockCorner.Bottom ? a.Bottom : a.Y, position == DockCorner.Bottom ? b.Bottom : b.Y); }
            else { Assert.Equal(work.Y + work.Height / 2, a.Y + a.Height / 2); Assert.Equal(position == DockCorner.Right ? a.Right : a.X, position == DockCorner.Right ? b.Right : b.X); }
        }
        Assert.Equal(0, (int)DockCorner.BottomRight);
    }
    [Fact]
    public void UndoBudgetEvictsOldestSnapshotsAndNewBranchesDiscardRedo()
    {
        var journal = new UndoJournal<int>(1, 20, states => states.Sum(x => (long)x), 6);
        journal.Push(2); journal.Push(3); journal.Push(4);
        Assert.Equal(4, journal.Current); Assert.False(journal.CanUndo);
        journal.Push(1); Assert.Equal(4, journal.Undo()); journal.Push(2); Assert.False(journal.CanRedo);
    }
}
