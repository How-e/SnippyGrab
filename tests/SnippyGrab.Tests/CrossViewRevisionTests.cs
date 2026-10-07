using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class CrossViewRevisionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SnippyGrab-revision-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void FailedRevisionCannotResurfaceAsAnotherHistoryCapture()
    {
        var fail = false;
        var repository = new CaptureRepository(root, (path, bytes) =>
        {
            if (fail && path.EndsWith("history.json", StringComparison.Ordinal)) throw new IOException("injected revision metadata failure");
            AtomicFile.Write(path, bytes);
        });
        var capture = repository.Add([1], 20, 30); var original = capture.FileName;
        var notifications = 0; repository.RevisionChanged += _ => notifications++;
        fail = true;
        Assert.Throws<IOException>(() => repository.Replace(capture, [2], 40, 50, original));
        Assert.Equal(0, notifications); Assert.Equal(original, capture.FileName);
        var reopened = new CaptureRepository(root); reopened.Load();
        Assert.Equal(capture.Id, Assert.Single(reopened.Captures).Id);
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(reopened.PathFor(reopened.Captures[0])));
        fail = false; repository.Replace(capture, [3], 60, 70, original);
        Assert.Equal(1, notifications);
        var final = new CaptureRepository(root); final.Load();
        Assert.Equal(capture.Id, Assert.Single(final.Captures).Id);
        Assert.Equal(new byte[] { 3 }, File.ReadAllBytes(final.PathFor(final.Captures[0])));
    }

    [Fact]
    public void StaleEditorCannotPublishPixelsOrRefreshAnyView()
    {
        var repository = new CaptureRepository(root); var capture = repository.Add([1], 20, 30);
        var original = capture.FileName;
        using var view = new CaptureViewLease(repository, capture);
        var revisions = new List<string>(); repository.RevisionChanged += c => revisions.Add(c.FileName);
        repository.Replace(capture, [2], 40, 50, original);
        Assert.Throws<InvalidOperationException>(() => repository.Replace(capture, [3], 60, 70, original));
        Assert.Equal(capture.FileName, Assert.Single(revisions));
        Assert.Equal(2, Directory.GetFiles(root, "capture-*.png").Length);
        repository.Cleanup(DateTimeOffset.UtcNow, 1, true);
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(repository.PathFor(capture)));
    }

    [Fact]
    public void ViewRefreshFailureCannotRollBackOrDeleteADurableRevision()
    {
        var repository = new CaptureRepository(root); var capture = repository.Add([1], 20, 30);
        using var view = new CaptureViewLease(repository, capture);
        repository.Changed += () => throw new InvalidOperationException("injected view refresh failure");
        Assert.Throws<InvalidOperationException>(() => repository.Replace(capture, [2], 40, 50));
        Assert.True(capture.Edited); Assert.False(repository.PersistencePending);
        var reopened = new CaptureRepository(root); reopened.Load();
        Assert.Equal(capture.FileName, Assert.Single(reopened.Captures).FileName);
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(reopened.PathFor(reopened.Captures[0])));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
