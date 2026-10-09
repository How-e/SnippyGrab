using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class CompositionTests
{
    [Theory]
    [InlineData(CompositionMode.Vertical, 7, 14)]
    [InlineData(CompositionMode.Horizontal, 12, 9)]
    [InlineData(CompositionMode.Grid, 16, 9)]
    public void HeterogeneousImagesRetainNativeSizes(CompositionMode mode, int width, int height)
    {
        var layout = Composition.Plan([(3, 3), (7, 9)], new(mode, 2, 2, CompositionAlignment.Center));
        Assert.Equal(width, layout.Width); Assert.Equal(height, layout.Height);
        Assert.Equal(3, layout.Rectangles[0].Width); Assert.Equal(9, layout.Rectangles[1].Height);
        Assert.All(layout.Rectangles, r => { Assert.InRange(r.X, 0, width - r.Width); Assert.InRange(r.Y, 0, height - r.Height); });
    }
    [Fact]
    public void OddGridPadsLastRowAndBudgetsFailBeforeAllocation()
    {
        var layout = Composition.Plan([(2, 2), (2, 2), (2, 2)], new(CompositionMode.Grid, 1)); Assert.Equal(5, layout.Width); Assert.Equal(5, layout.Height);
        Assert.Equal(new PixelRect(0, 3, 2, 2), layout.Rectangles[2]);
        Assert.Throws<InvalidDataException>(() => Composition.Plan([(10000, 8000)], new()));
        Assert.Throws<InvalidDataException>(() => Composition.Plan([(int.MaxValue, 2), (int.MaxValue, 2)], new(CompositionMode.Horizontal)));
        Assert.Throws<InvalidDataException>(() => Composition.Plan([(0, 2)], new()));
    }
}
