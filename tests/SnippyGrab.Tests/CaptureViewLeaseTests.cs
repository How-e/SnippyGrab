using SnippyGrab.Core;
namespace SnippyGrab.Tests;

public sealed class CaptureViewLeaseTests
{
    [Fact]
    public void TwoViewsFollowRevisionsAndOnlyLastCloseReleasesCurrentSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-two-pins-" + Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new CaptureRepository(root); var record = repository.Add([1], 20, 30);
            using var first = new CaptureViewLease(repository, record);
            using var second = new CaptureViewLease(repository, record);
            repository.SetPinned(record, false);
            repository.Replace(record, [2], 40, 50);
            repository.Replace(record, [3], 60, 70);
            first.Dispose();
            Assert.Equal(2, repository.Cleanup(DateTimeOffset.UtcNow, 1, true));
            Assert.Equal(new byte[] { 3 }, File.ReadAllBytes(repository.PathFor(record)));
            second.Dispose(); Assert.Equal(1, repository.Cleanup(DateTimeOffset.UtcNow, 1, true));
            var other = repository.Add([4], 20, 30); repository.Replace(other, [5], 30, 40);
            Assert.Equal(2, repository.Cleanup(DateTimeOffset.UtcNow, 1, true));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void DetachedViewSurvivesUnpinClearAndFollowsRevisionUntilClose()
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-pin-" + Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new CaptureRepository(root); var record = repository.Add([1], 20, 30); record.Pinned = true;
            var view = new CaptureViewLease(repository, record); record.Pinned = false; repository.Persist();
            Assert.Equal(0, repository.Cleanup(DateTimeOffset.UtcNow, 1, true));
            var old = repository.PathFor(record); repository.Replace(record, [2], 40, 50);
            Assert.Equal(1, repository.Cleanup(DateTimeOffset.UtcNow, 1, true)); Assert.False(File.Exists(old)); Assert.True(File.Exists(repository.PathFor(record)));
            view.Dispose(); view.Dispose(); Assert.Equal(1, repository.Cleanup(DateTimeOffset.UtcNow, 1, true));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
