using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class TimingSummaryTests
{
    [Fact]
    public void UsesNearestRankAndRetainsOutliers()
    {
        var summary = TimingSummary.From(Enumerable.Range(1, 100).Reverse().Select(n => (double)n));
        Assert.Equal(new TimingSummary(100, 1, 50, 95, 100), summary);
        Assert.Throws<ArgumentException>(() => TimingSummary.From([]));
        Assert.Throws<ArgumentException>(() => TimingSummary.From([double.NaN]));
    }
}
