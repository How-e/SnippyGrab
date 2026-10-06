using SnippyGrab.Core;
namespace SnippyGrab.Tests;

public sealed class HistoryBehaviorTests
{
    [Fact]
    public void HistoryPagesAreBoundedAndCaptureTimeOrdered()
    {
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var captures = Enumerable.Range(0, 450).Select(i => new CaptureRecord { CreatedUtc = start.AddMinutes(i) }).ToArray();
        Assert.Equal(200, HistoryPage.Read(captures, 0).Length); Assert.Equal(captures[449].Id, HistoryPage.Read(captures, 0)[0].Id);
        Assert.Equal(captures[249].Id, HistoryPage.Read(captures, 1)[0].Id); Assert.Equal(50, HistoryPage.Read(captures, 2).Length);
    }
    [Fact]
    public void OptionalHistoryPinsPersistDismissDoesNotDeleteAndFailuresRollBack()
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-history-behavior-" + Guid.NewGuid().ToString("N"));
        try
        {
            bool fail = false; var repository = new CaptureRepository(root, (path, bytes) => { if (fail) throw new IOException(); AtomicFile.Write(path, bytes); }) { HistoryEnabled = false };
            var capture = repository.Add([1], 20, 30); var changes = 0; repository.Changed += () => changes++;
            repository.SetPinned(capture, true); repository.Dismiss([capture]); Assert.True(File.Exists(repository.PathFor(capture))); Assert.Equal(2, changes);
            fail = true; Assert.Throws<IOException>(() => repository.SetPinned(capture, false)); Assert.True(capture.Pinned); fail = false; repository.Persist();
            var reopened = new CaptureRepository(root) { HistoryEnabled = false }; reopened.Load(); var pin = Assert.Single(reopened.Captures); Assert.True(pin.Pinned && pin.Dismissed);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
