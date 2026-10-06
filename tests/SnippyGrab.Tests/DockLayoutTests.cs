using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class DockLayoutTests
{
    [Theory]
    [InlineData(DockCorner.BottomRight, DockOrientation.Vertical, true)]
    [InlineData(DockCorner.BottomLeft, DockOrientation.Vertical, true)]
    [InlineData(DockCorner.TopRight, DockOrientation.Vertical, false)]
    [InlineData(DockCorner.TopLeft, DockOrientation.Vertical, false)]
    [InlineData(DockCorner.BottomRight, DockOrientation.Horizontal, true)]
    [InlineData(DockCorner.TopRight, DockOrientation.Horizontal, true)]
    [InlineData(DockCorner.BottomLeft, DockOrientation.Horizontal, false)]
    [InlineData(DockCorner.TopLeft, DockOrientation.Horizontal, false)]
    public void PrimaryCardStaysAtAnchoredEnd(DockCorner corner, DockOrientation orientation, bool reverse)
    {
        Assert.Equal(reverse, DockLayout.Reverse(orientation, corner));
        var work = new PixelRect(-1920, -221, 1920, 1080);
        var collapsed = DockLayout.Place(work, 240, 140, corner);
        var expanded = DockLayout.Place(work, orientation == DockOrientation.Horizontal ? 720 : 240, orientation == DockOrientation.Vertical ? 420 : 140, corner);
        if (orientation == DockOrientation.Vertical)
            Assert.Equal(reverse ? collapsed.Bottom : collapsed.Y, reverse ? expanded.Bottom : expanded.Y);
        else Assert.Equal(reverse ? collapsed.Right : collapsed.X, reverse ? expanded.Right : expanded.X);
    }

    [Fact]
    public void OversizedAndNearlyFullShelfStayInsideWorkArea()
    {
        var work = new PixelRect(-400, -300, 800, 600);
        foreach (var corner in Enum.GetValues<DockCorner>())
        {
            Assert.Equal(work, DockLayout.Place(work, 1000, 900, corner));
            var nearly = DockLayout.Place(work, 795, 595, corner);
            Assert.Equal(nearly, nearly.Intersect(work));
        }
    }

    [Theory]
    [InlineData(0, 1, "1023")]
    [InlineData(1, 0, "1023")]
    [InlineData(0, 3, "1230")]
    [InlineData(3, 0, "3012")]
    [InlineData(1, 3, "0231")]
    [InlineData(3, 1, "0312")]
    public void DropTakesTargetsOriginalPositionInBothDirections(int from, int target, string expected)
    {
        var captures = Enumerable.Range(0, 4).Select(i => new CaptureRecord { Monitor = i.ToString() }).ToList();
        Assert.True(ShelfOrder.Move(captures, captures[from].Id, captures[target].Id));
        Assert.Equal(expected, string.Concat(captures.Select(c => c.Monitor)));
    }
}
