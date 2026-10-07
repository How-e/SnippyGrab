using SnippyGrab.Core;
namespace SnippyGrab.Tests;

public sealed class TransferLifecycleTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SnippyGrab-transfer-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public void SupersededRevisionsDoNotReappearInHistoryAfterRestart()
    {
        var repository = new CaptureRepository(root); var record = repository.Add([1], 2, 3);
        using (repository.Lease([record], true)) repository.Replace(record, [2], 4, 5);
        var reopened = new CaptureRepository(root); reopened.Load(); Assert.Equal(record.Id, Assert.Single(reopened.Captures).Id);
        Assert.Equal(record.FileName, reopened.Captures[0].FileName);
    }
    [Fact]
    public void GraceExtendsAtReleaseAndSurvivesCrashRestartAndRevision()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z"); var repository = new CaptureRepository(root, utcNow: () => now);
        var record = repository.Add([1], 2, 3); var oldPath = repository.PathFor(record);
        using (repository.Lease([record], true))
        { now = now.AddHours(30); Assert.Equal(0, repository.Cleanup(now, 1, true)); repository.Replace(record, [2], 4, 5); }
        var reopened = new CaptureRepository(root, utcNow: () => now); reopened.Load();
        reopened.Cleanup(now.AddHours(23), 1, true); Assert.True(File.Exists(oldPath));
        reopened.Cleanup(now.AddHours(25), 1, true); Assert.False(File.Exists(oldPath));
    }
    [Fact]
    public void SessionExitOnlyClearsCurrentCohort()
    {
        var first = new CaptureRepository(root); var old = first.Add([1], 2, 3);
        var next = new CaptureRepository(root); next.Load(); var current = next.Add([2], 2, 3);
        Assert.Equal(1, next.CleanupSession(DateTimeOffset.UtcNow)); Assert.True(File.Exists(first.PathFor(old))); Assert.False(File.Exists(next.PathFor(current)));
    }
    [Fact]
    public void FailedTransferPersistenceReleasesLeaseForRetry()
    {
        bool fail = false; var repository = new CaptureRepository(root, (path, bytes) => { if (fail) throw new IOException(); AtomicFile.Write(path, bytes); });
        var record = repository.Add([1], 2, 3); fail = true;
        Assert.Throws<IOException>(() => repository.Lease([record], true)); fail = false; repository.Persist();
        Assert.Equal(1, repository.Cleanup(DateTimeOffset.UtcNow, 1, true));
    }
    [Fact]
    public void FailedReleasePersistenceBlocksCleanupUntilExtendedGraceIsDurable()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z"); var fail = false;
        var repository = new CaptureRepository(root, (path, bytes) => { if (fail) throw new IOException(); AtomicFile.Write(path, bytes); }, () => now);
        var capture = repository.Add([1, 2, 3], 2, 3);
        var first = repository.Lease([capture], true); var second = repository.Lease([capture], true);
        now = now.AddHours(30); first.Dispose(); fail = true; second.Dispose(); second.Dispose();
        Assert.True(repository.PersistencePending);
        Assert.Equal(0, repository.Cleanup(now.AddHours(25), 1, true));
        fail = false; repository.Persist();
        var reopened = new CaptureRepository(root, utcNow: () => now); reopened.Load();
        Assert.Equal(0, reopened.Cleanup(now.AddHours(23), 1, true));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(reopened.PathFor(capture)));
        Assert.Equal(1, reopened.Cleanup(now.AddHours(24), 1, true));
    }
    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    [InlineData(168)]
    [InlineData(-1)]
    public void ClockDrivenRetention(int hours)
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z"); var repository = new CaptureRepository(root, utcNow: () => now); repository.Add([1], 2, 3);
        Assert.Equal(hours < 0 ? 0 : 1, repository.Cleanup(now.AddHours(168), hours));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
