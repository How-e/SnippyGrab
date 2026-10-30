using SnippyGrab.App.Views;
using SnippyGrab.Core;

namespace SnippyGrab.IntegrationTests;

public sealed class WindowPlacementTests
{
    [Theory]
    [InlineData(-2560, -1440, 2560, 1400)]
    [InlineData(0, 48, 1920, 1032)]
    [InlineData(80, 0, 1000, 720)]
    [InlineData(1920, -400, 800, 560)]
    public void OversizedWindowsFitWorkAreaAndKeepTitlebarReachable(int x, int y, int width, int height)
    {
        var work = new PixelRect(x, y, width, height);
        foreach (var center in new[] { false, true })
        {
            var result = WindowPlacement.Constrain(work, new(-30000, -30000, 2640, 1800), center);
            Assert.Equal(result, result.Intersect(work));
            Assert.True(result.Height < height); Assert.True(result.Width < width);
            if (center) { Assert.Equal(x + (width - result.Width) / 2, result.X); Assert.Equal(y + (height - result.Height) / 2, result.Y); }
        }
    }
    [Fact]
    public void MovingToSmallerWorkAreaPreservesVisiblePositionWithoutRecentering()
    {
        Assert.Equal(new PixelRect(-1200, 80, 500, 400), WindowPlacement.Constrain(new(-1280, 40, 1280, 984), new(-1200, 80, 500, 400), false));
        Assert.Equal(new PixelRect(700, 344, 500, 400), WindowPlacement.Constrain(new(0, 24, 1200, 720), new(1800, 1000, 500, 400), false));
    }
}
