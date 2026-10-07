using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class CaptureRestoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SnippyGrab-restore-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void FailedBatchRestoreRollsBackEveryLifetimeAndRetriesAfterRestart()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var fail = false;
        var repository = new CaptureRepository(root, (path, bytes) => { if (fail) throw new IOException("injected metadata failure"); AtomicFile.Write(path, bytes); }, () => now);
        var first = repository.Add([1], 20, 30); var second = repository.Add([2], 20, 30);
        repository.Dismiss([first, second]); now = now.AddDays(3); fail = true;
        Assert.Throws<IOException>(() => repository.Restore([first, second, first], now));
        Assert.All(repository.Captures, c => { Assert.True(c.Dismissed); Assert.Null(c.RestoredUtc); });
        Assert.Equal(0, repository.Cleanup(now, 1));
        var reopened = new CaptureRepository(root, utcNow: () => now); reopened.Load();
        reopened.Restore(reopened.Captures, now);
        Assert.All(reopened.Captures, c =>
        {
            Assert.Equal(now.AddDays(-3), c.CreatedUtc);
            Assert.True(CaptureLifetime.Visible(c, 30, now));
            Assert.False(CaptureLifetime.Visible(c, 30, now.AddMinutes(30)));
        });
        Assert.Equal(0, reopened.Cleanup(now.AddMinutes(59), 1));
        Assert.Equal(2, reopened.Cleanup(now.AddHours(1), 1));
    }

    [Fact]
    public void RepeatedRestoreRestartsVisibilityWithoutReorderingCaptureHistory()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var repository = new CaptureRepository(root, utcNow: () => now);
        var oldest = repository.Add([1], 20, 30); now = now.AddMinutes(1);
        var newest = repository.Add([2], 20, 30); now = now.AddDays(2);
        repository.Restore([oldest], now); now = now.AddMinutes(29); repository.Restore([oldest], now);
        Assert.Equal(new[] { newest.Id, oldest.Id }, HistoryPage.Read(repository.Captures, 0).Select(c => c.Id));
        Assert.True(CaptureLifetime.Visible(oldest, 30, now.AddMinutes(29)));
        Assert.False(CaptureLifetime.Visible(oldest, 30, now.AddMinutes(30)));
        Assert.False(CaptureLifetime.Visible(newest, 30, now));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
