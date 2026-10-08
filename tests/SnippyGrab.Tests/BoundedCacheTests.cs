using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class BoundedCacheTests
{
    [Fact]
    public void RevisionInvalidationKeepsOtherWorkingThumbnails()
    {
        var cache = new BoundedCache<(string File, int Pixels), object>(3);
        var unrelated = cache.GetOrAdd(("other", 100), () => new object());
        cache.GetOrAdd(("old", 100), () => new object());
        cache.GetOrAdd(("old", 200), () => new object());
        cache.RemoveWhere(key => key.File == "old");
        Assert.Equal(1, cache.Count);
        Assert.Same(unrelated, cache.GetOrAdd(("other", 100), () => throw new InvalidOperationException()));
        var revision = cache.GetOrAdd(("new", 100), () => new object());
        Assert.Same(revision, cache.GetOrAdd(("new", 100), () => throw new InvalidOperationException()));
    }

    [Fact]
    public void RecentlyUsedThumbnailSurvivesScrollingEviction()
    {
        var cache = new BoundedCache<string, object>(2);
        var first = cache.GetOrAdd("first", () => new object());
        cache.GetOrAdd("second", () => new object());
        Assert.Same(first, cache.GetOrAdd("first", () => throw new InvalidOperationException()));
        cache.GetOrAdd("third", () => new object());
        Assert.Equal(2, cache.Count); Assert.False(cache.TryGetValue("second", out _));
        Assert.True(cache.TryGetValue("first", out _));
    }

    [Fact]
    public void ThousandsOfShelfItemsCannotGrowWorkingCache()
    {
        var cache = new BoundedCache<int, object>(12);
        for (var i = 0; i < 2000; i++) { cache.GetOrAdd(i, () => new object()); Assert.InRange(cache.Count, 1, 12); }
        cache.RemoveWhere(i => i < 1999); Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void FailedDecodeDoesNotEvictUsableThumbnailOrCacheFailure()
    {
        var cache = new BoundedCache<string, object>(1); var usable = cache.GetOrAdd("good", () => new object());
        Assert.Throws<IOException>(() => cache.GetOrAdd("bad", () => throw new IOException()));
        Assert.Same(usable, cache.GetOrAdd("good", () => new object()));
        var retried = cache.GetOrAdd("bad", () => new object());
        Assert.Same(retried, cache.GetOrAdd("bad", () => throw new InvalidOperationException()));
    }
}
