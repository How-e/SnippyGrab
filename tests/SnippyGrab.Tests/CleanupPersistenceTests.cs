using System.Text.Json;
using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class CleanupPersistenceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SnippyGrab-cleanup-persistence-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task UnchangedCleanupDoesNotWriteOrNotifyInEitherPath()
    {
        var writes = 0; var changes = 0;
        var repo = new CaptureRepository(root, (path, bytes) => { writes++; AtomicFile.Write(path, bytes); });
        var capture = await repo.AddAsync([1], 1, 1); writes = 0; repo.Changed += () => changes++;
        Assert.Equal(0, repo.Cleanup(capture.CreatedUtc, 24));
        Assert.Equal(0, await repo.CleanupAsync(capture.CreatedUtc, 24));
        Assert.Equal(0, writes); Assert.Equal(0, changes); Assert.Single(repo.Captures);
    }
    [Fact]
    public async Task ExpiredProtectionIsPersistedEvenWhenPinnedCaptureIsNotRemoved()
    {
        var now = DateTimeOffset.UtcNow; var writes = 0; var changes = 0;
        var repo = new CaptureRepository(root, (path, bytes) => { writes++; AtomicFile.Write(path, bytes); }, () => now);
        var capture = await repo.AddAsync([1], 1, 1); await repo.SetPinnedAsync(capture, true);
        await using (await repo.TransferLeaseAsync([capture])) { }
        now = now.AddHours(25); writes = 0; repo.Changed += () => changes++;
        Assert.Equal(0, await repo.CleanupAsync(now, -1)); Assert.Equal(1, writes); Assert.Equal(1, changes);
        using var saved = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "history.json")));
        Assert.Empty(saved.RootElement.GetProperty("ProtectedUntil").EnumerateObject());
        Assert.Equal(0, await repo.CleanupAsync(now, -1)); Assert.Equal(1, writes); Assert.Equal(1, changes);
        Assert.True(File.Exists(repo.PathFor(capture)));
    }
    [Fact]
    public async Task CaptureDeletionStillCommitsOneSnapshotAndNotifies()
    {
        var writes = 0; var changes = 0;
        var repo = new CaptureRepository(root, (path, bytes) => { writes++; AtomicFile.Write(path, bytes); });
        var capture = await repo.AddAsync([1], 1, 1); writes = 0; repo.Changed += () => changes++;
        Assert.Equal(1, await repo.CleanupAsync(capture.CreatedUtc.AddHours(25), 24));
        Assert.Equal(1, writes); Assert.Equal(1, changes);
        var reopened = new CaptureRepository(root); reopened.Load(); Assert.Empty(reopened.Captures);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
