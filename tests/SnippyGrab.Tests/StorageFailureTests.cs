using SnippyGrab.Core;
namespace SnippyGrab.Tests;

public sealed class StorageFailureTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SnippyGrab-fault-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public void MetadataFailureKeepsPixelsAndRetryableRecord()
    {
        bool fail = true;
        var repository = new CaptureRepository(root, (path, bytes) => { if (fail && path.EndsWith("history.json")) throw new IOException("injected disk full"); AtomicFile.Write(path, bytes); });
        var capture = repository.Add([1, 2], 30, 40);
        Assert.True(repository.PersistencePending); Assert.Equal(new byte[] { 1, 2 }, File.ReadAllBytes(repository.PathFor(capture)));
        Assert.Equal(0, repository.Cleanup(DateTimeOffset.UtcNow, 1, true));
        fail = false; repository.Persist(); Assert.False(repository.PersistencePending);
        var reopened = new CaptureRepository(root); reopened.Load(); Assert.Equal(capture.Id, Assert.Single(reopened.Captures).Id);
    }
    [Fact]
    public void ImageWriteFailureDoesNotCreateRecord()
    {
        var repository = new CaptureRepository(root, (_, _) => throw new UnauthorizedAccessException());
        Assert.Throws<UnauthorizedAccessException>(() => repository.Add([1], 2, 3)); Assert.Empty(repository.Captures);
    }
    [Fact]
    public void UnchangedStartupDoesNotRequireRegistryWriteAndCacheProbeIsIsolated()
    {
        var saved = false;
        SettingsTransaction.Apply(new(), false, _ => throw new UnauthorizedAccessException(), _ => saved = true); Assert.True(saved);
        var repository = new CaptureRepository(root); repository.ProbeWritable(); Assert.Empty(Directory.GetFiles(root));
        Assert.Throws<IOException>(() => new CaptureRepository(root, (_, _) => throw new IOException()).ProbeWritable());
    }
    [Fact]
    public void StartupAndSaveFailuresRollBackRegistration()
    {
        bool enabled = false;
        Assert.Throws<IOException>(() => SettingsTransaction.Apply(new() { LaunchOnStartup = true }, false, value => enabled = value, _ => throw new IOException()));
        Assert.False(enabled);
        var saves = 0;
        var failure = Assert.Throws<AggregateException>(() => SettingsTransaction.Apply(new() { LaunchOnStartup = true }, false, _ => throw new UnauthorizedAccessException(), _ => saves++));
        Assert.All(failure.InnerExceptions, error => Assert.IsType<UnauthorizedAccessException>(error));
        Assert.Equal(0, saves);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
