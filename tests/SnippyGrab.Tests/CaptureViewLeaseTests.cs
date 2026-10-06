using SnippyGrab.Core;
namespace SnippyGrab.Tests;
public sealed class CaptureViewLeaseTests
{
    [Fact] public void DetachedViewSurvivesUnpinClearAndFollowsRevisionUntilClose()
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
