using System.Reflection;
using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class RepositoryAsyncTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SnippyGrab-async-tests-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task CaptureChurnReleasesSessionNamesButKeepsPinsAndTransfers()
    {
        var repo = new CaptureRepository(root);
        var pin = await repo.AddAsync([1], 1, 1); await repo.SetPinnedAsync(pin, true);
        var transfer = await repo.AddAsync([2], 1, 1);
        await using (await repo.TransferLeaseAsync([transfer]))
        {
            for (var i = 0; i < 100; i++) { await repo.AddAsync([3], 1, 1); await repo.CleanupAsync(DateTimeOffset.UtcNow, -1, clear: true); }
            Assert.Equal(2, repo.Captures.Count); Assert.True(File.Exists(repo.PathFor(pin))); Assert.True(File.Exists(repo.PathFor(transfer)));
            var names = (HashSet<string>)typeof(CaptureRepository).GetField("sessionFiles", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(repo)!;
            Assert.Equal(2, names.Count);
        }
        var reopened = new CaptureRepository(root); reopened.Load(); Assert.Equal(2, reopened.Captures.Count);
        Assert.Equal(0, reopened.Cleanup(DateTimeOffset.UtcNow, 1, true));
    }
    [Fact]
    public async Task PendingEditIsInvisibleAndFailedCommitPreservesOldRevision()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim(); var block = false; var fail = false;
        var repo = new CaptureRepository(root, (path, bytes) =>
        {
            if (block && path.EndsWith("history.json")) { entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); if (fail) throw new IOException("injected metadata failure"); }
            AtomicFile.Write(path, bytes);
        });
        var record = await repo.AddAsync([1], 1, 1); var prior = record.FileName; var revisions = 0; repo.RevisionChanged += _ => revisions++;
        block = true; fail = true; var edit = repo.ReplaceAsync(record, [2], 1, 1, prior);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        Assert.Equal(prior, record.FileName); Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(repo.PathFor(record))); Assert.Equal(0, revisions);
        release.Set(); await Assert.ThrowsAsync<IOException>(() => edit);
        Assert.Equal(prior, record.FileName); Assert.Single(Directory.GetFiles(root, "capture-*.png")); Assert.True(repo.PersistencePending);
        block = false; await repo.PersistAsync(); await repo.ReplaceAsync(record, [3], 1, 1, prior);
        Assert.NotEqual(prior, record.FileName); Assert.Equal(1, revisions);
        var loaded = new CaptureRepository(root); loaded.Load(); Assert.Equal(record.FileName, Assert.Single(loaded.Captures).FileName);
        Assert.Equal(new byte[] { 3 }, File.ReadAllBytes(loaded.PathFor(loaded.Captures[0])));
    }
    [Fact]
    public async Task ConcurrentAsyncWritesKeepEveryCaptureAndDurableTransferDeadline()
    {
        var repo = new CaptureRepository(root);
        var records = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => repo.AddAsync([(byte)i], 1, 1)));
        await using (await repo.TransferLeaseAsync([records[0]])) { await repo.SetPinnedAsync(records[1], true); }
        await repo.DrainAsync();
        var loaded = new CaptureRepository(root); loaded.Load(); Assert.Equal(20, loaded.Captures.Count);
        Assert.True(loaded.Captures.Single(c => c.Id == records[1].Id).Pinned);
        Assert.Equal(18, await loaded.CleanupAsync(DateTimeOffset.UtcNow, -1, true)); Assert.Equal(2, loaded.Captures.Count);
    }
    [Fact]
    public async Task QueuedExportMetadataCannotAttachOldPixelsToANewRevision()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim(); var blocked = false;
        var repo = new CaptureRepository(root, (path, bytes) =>
        {
            if (blocked && path.EndsWith("history.json")) { entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); }
            AtomicFile.Write(path, bytes);
        });
        var record = await repo.AddAsync([1], 1, 1); var prior = record.FileName;
        blocked = true; var pin = repo.SetPinnedAsync(record, true); Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        var edit = repo.ReplaceAsync(record, [2], 1, 1, prior);
        var remember = repo.RememberExportAsync(record, Path.Combine(root, "old.png"), prior);
        release.Set(); await pin; await edit;
        await Assert.ThrowsAsync<InvalidOperationException>(() => remember);
        Assert.False(record.Saved); Assert.Equal("", record.ExportPath);
        var loaded = new CaptureRepository(root); loaded.Load(); Assert.False(Assert.Single(loaded.Captures).Saved);
    }
    [Fact]
    public async Task CleanupCannotDeleteAnUncommittedUnpinThatRollsBack()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim(); var fail = 0;
        var repo = new CaptureRepository(root, (path, bytes) =>
        {
            if (path.EndsWith("history.json") && Interlocked.Exchange(ref fail, 0) == 1)
            {
                entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                throw new IOException("injected unpin commit failure");
            }
            AtomicFile.Write(path, bytes);
        });
        var record = await repo.AddAsync([1], 1, 1); await repo.SetPinnedAsync(record, true);
        fail = 1; var unpin = repo.SetPinnedAsync(record, false); Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        Assert.False(record.Pinned);
        Assert.Equal(0, repo.Cleanup(DateTimeOffset.UtcNow, -1, clear: true));
        var cleanup = repo.CleanupAsync(DateTimeOffset.UtcNow, -1, clear: true);
        Assert.True(File.Exists(repo.PathFor(record)));
        release.Set(); await Assert.ThrowsAsync<IOException>(() => unpin);
        Assert.Equal(0, await cleanup); Assert.True(record.Pinned); Assert.True(File.Exists(repo.PathFor(record)));
        var loaded = new CaptureRepository(root); loaded.Load(); Assert.True(Assert.Single(loaded.Captures).Pinned);
    }
    [Fact]
    public async Task PixelWriteFailureReleasesAsyncQueueForRetry()
    {
        var fail = true; var repo = new CaptureRepository(root, (path, bytes) => { if (fail) throw new IOException("injected pixel-write failure"); AtomicFile.Write(path, bytes); });
        await Assert.ThrowsAsync<IOException>(() => repo.AddAsync([1], 1, 1)); Assert.Empty(repo.Captures);
        fail = false; var record = await repo.AddAsync([2], 1, 1).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(repo.PathFor(record)));
    }
    [Fact]
    public void OversizedEncodingFailsBeforeBufferGrowthOrRepositoryWrite()
    {
        using var stream = new BoundedImageStream(); stream.Position = ImageLimits.MaxEncodedBytes;
        Assert.Throws<InvalidDataException>(() => stream.WriteByte(1)); Assert.Equal(0, stream.Capacity); Assert.True(stream.LimitExceeded);
        var writes = 0; var repo = new CaptureRepository(root, (_, _) => writes++);
        Assert.Throws<InvalidDataException>(() => repo.Add(new byte[ImageLimits.MaxEncodedBytes + 1], 1, 1)); Assert.Equal(0, writes); Assert.Empty(repo.Captures);
        Assert.Throws<InvalidDataException>(() => ImageLimits.Validate(80_000_000, 24, "Blur"));
    }
    [Fact]
    public async Task BatchPinTogglesMixedSelectionInOneCommitAndRollsBackTogether()
    {
        var writes = 0; var changes = 0; var fail = false;
        var repo = new CaptureRepository(root, (path, bytes) => { if (path.EndsWith("history.json")) { writes++; if (fail) throw new IOException("injected metadata failure"); } AtomicFile.Write(path, bytes); });
        var first = await repo.AddAsync([1], 1, 1); var second = await repo.AddAsync([2], 1, 1); await repo.SetPinnedAsync(first, true);
        writes = 0; repo.Changed += () => changes++;
        await repo.TogglePinnedAsync([first, second, first]); Assert.Equal(1, writes); Assert.Equal(1, changes); Assert.False(first.Pinned); Assert.True(second.Pinned);
        var reopened = new CaptureRepository(root); reopened.Load(); Assert.True(reopened.Captures.Single(c => c.Id == second.Id).Pinned);
        fail = true; await Assert.ThrowsAsync<IOException>(() => repo.TogglePinnedAsync([first, second])); Assert.False(first.Pinned); Assert.True(second.Pinned); Assert.Equal(1, changes);
        fail = false; await repo.PersistAsync();
        await repo.SetPinnedAsync([first, second], true); Assert.True(first.Pinned && second.Pinned);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SetPinnedAsync([first, new CaptureRecord()], false)); Assert.True(first.Pinned && second.Pinned);
    }
    [Fact]
    public async Task CleanupSlicesAndConcurrentAddsKeepPinsAndHistoryConsistent()
    {
        var now = DateTimeOffset.UtcNow.AddDays(-2);
        var repo = new CaptureRepository(root, utcNow: () => now); var pin = await repo.AddAsync([1], 1, 1); await repo.SetPinnedAsync(pin, true);
        for (var i = 0; i < 30; i++) await repo.AddAsync([2], 1, 1);
        now = DateTimeOffset.UtcNow;
        var cleanup = repo.CleanupAsync(now, 1, batchSize: 1);
        var added = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(() => repo.AddAsync([(byte)i], 1, 1))));
        await cleanup; await repo.CleanupAsync(now, 1);
        var reopened = new CaptureRepository(root); reopened.Load();
        Assert.Equal(21, reopened.Captures.Count); Assert.True(reopened.Captures.Single(c => c.Id == pin.Id).Pinned);
        Assert.All(added, c => Assert.Contains(reopened.Captures, saved => saved.Id == c.Id)); Assert.True(File.Exists(repo.PathFor(pin)));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
