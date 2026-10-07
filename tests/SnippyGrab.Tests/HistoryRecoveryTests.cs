using System.Text.Json;
using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class HistoryRecoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SnippyGrab-recovery-" + Guid.NewGuid().ToString("N"));
    private static readonly byte[] Pixels = [137, 80, 78, 71];

    [Theory]
    [InlineData("corrupt")]
    [InlineData("truncated")]
    [InlineData("oversized")]
    [InlineData("newer")]
    public void RecoveryRestoresNewPinsAndTransferMetadataAcrossLaunches(string kind)
    {
        var original = new CaptureRepository(root);
        var oldPin = original.Add(Pixels, 20, 30); oldPin.Pinned = true; original.Persist();
        var metadata = Path.Combine(root, "history.json");
        var invalid = kind switch
        {
            "truncated" => "{\"Captures\":[",
            "oversized" => new string(' ', 4 * 1024 * 1024 + 1),
            "newer" => JsonSerializer.Serialize(new RepositoryState { SchemaVersion = 3, Captures = [oldPin] }),
            _ => "broken history"
        };
        File.WriteAllText(metadata, invalid);
        var first = new CaptureRepository(root); first.Load();
        Assert.True(first.CleanupBlocked);
        Assert.True(Assert.Single(first.Captures).Pinned);
        var newPin = first.Add(Pixels, 40, 50); newPin.Pinned = true;
        var transfer = first.Add(Pixels, 60, 70);
        using (first.Lease([transfer], transfer: true)) { }
        first.Persist();
        var second = new CaptureRepository(root); second.Load();
        Assert.True(second.CleanupBlocked); Assert.Equal(invalid, File.ReadAllText(metadata));
        Assert.Contains(second.Captures, c => c.Id == newPin.Id && c.Pinned);
        Assert.Contains(second.Captures, c => c.Id == transfer.Id && !c.Pinned);
        Assert.Equal(0, second.Cleanup(DateTimeOffset.UtcNow.AddYears(1), 1, clear: true));
        second.ConfirmHistoryRecovery(); Assert.False(second.CleanupBlocked);
        Assert.Equal(invalid, File.ReadAllText(Assert.Single(Directory.GetFiles(root, "history.invalid-*.json"))));
        var third = new CaptureRepository(root); third.Load(); Assert.False(third.CleanupBlocked);
        Assert.Contains(third.Captures, c => c.Id == newPin.Id && c.Pinned);
        Assert.Equal(0, third.Cleanup(DateTimeOffset.UtcNow.AddHours(23), 1, clear: true));
        Assert.Equal(1, third.Cleanup(DateTimeOffset.UtcNow.AddHours(25), 1, clear: true));
        Assert.True(File.Exists(third.PathFor(oldPin))); Assert.True(File.Exists(third.PathFor(newPin)));
    }

    [Fact]
    public void RecoveryDoesNotDropPossiblePinsBeyondOrdinaryOrphanScanLimit()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "history.json"), "invalid");
        for (var i = 0; i < 2001; i++)
            File.WriteAllBytes(Path.Combine(root, "capture-" + Guid.NewGuid().ToString("N") + ".png"), Pixels);
        var repository = new CaptureRepository(root); repository.Load();
        Assert.Equal(2001, repository.Captures.Count); Assert.All(repository.Captures, c => Assert.True(c.Pinned));
        repository.ConfirmHistoryRecovery();
        var reopened = new CaptureRepository(root); reopened.Load();
        Assert.Equal(2001, reopened.Captures.Count);
        Assert.Equal(0, reopened.Cleanup(DateTimeOffset.UtcNow.AddYears(1), 1, true));
    }

    [Fact]
    public void OversizedRecoveredStateCannotEnableCleanupOrReplaceOriginal()
    {
        var repository = new CaptureRepository(root); repository.Add(Pixels, 10, 10);
        var metadata = Path.Combine(root, "history.json"); File.WriteAllText(metadata, "invalid"); repository.Load();
        repository.Captures[0].Monitor = new string('x', 4 * 1024 * 1024);
        Assert.Throws<InvalidDataException>(repository.ConfirmHistoryRecovery);
        Assert.True(repository.CleanupBlocked); Assert.Equal("invalid", File.ReadAllText(metadata));
    }

    [Fact]
    public void HistoryDisabledStillProtectsUnknownOldPins()
    {
        var repository = new CaptureRepository(root); var pin = repository.Add(Pixels, 10, 10);
        File.WriteAllText(Path.Combine(root, "history.json"), "invalid");
        var first = new CaptureRepository(root) { HistoryEnabled = false }; first.Load(); first.Persist();
        var second = new CaptureRepository(root) { HistoryEnabled = false }; second.Load(); second.ConfirmHistoryRecovery();
        var third = new CaptureRepository(root) { HistoryEnabled = false }; third.Load();
        Assert.True(Assert.Single(third.Captures).Pinned);
        Assert.Equal(0, third.Cleanup(DateTimeOffset.UtcNow.AddYears(1), 1, true));
        Assert.True(File.Exists(third.PathFor(pin)));
    }

    [Fact]
    public void InvalidRecoverySidecarIsPreservedBeforeReplacement()
    {
        var repository = new CaptureRepository(root); repository.Add(Pixels, 10, 10);
        File.WriteAllText(Path.Combine(root, "history.json"), "invalid original");
        var sidecar = Path.Combine(root, "history-recovered.json"); File.WriteAllText(sidecar, "invalid recovery");
        repository.Load(); repository.Persist();
        Assert.Equal("invalid recovery", File.ReadAllText(Assert.Single(Directory.GetFiles(root, "history-recovered.invalid-*.json"))));
        var reopened = new CaptureRepository(root); reopened.Load();
        Assert.True(reopened.CleanupBlocked); Assert.True(Assert.Single(reopened.Captures).Pinned);
    }

    [Fact]
    public void FailedConfirmationKeepsCleanupBlockedAndRecoveryRetryable()
    {
        var repository = new CaptureRepository(root); var capture = repository.Add(Pixels, 10, 10);
        var metadata = Path.Combine(root, "history.json"); File.WriteAllText(metadata, "invalid");
        repository.Load();
        using (var locked = new FileStream(metadata, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = Record.Exception(repository.ConfirmHistoryRecovery);
            Assert.True(error is IOException or UnauthorizedAccessException);
            Assert.True(repository.CleanupBlocked);
            Assert.Equal(0, repository.Cleanup(DateTimeOffset.UtcNow.AddYears(1), 1, true));
        }
        var reopened = new CaptureRepository(root); reopened.Load(); reopened.ConfirmHistoryRecovery();
        Assert.False(reopened.CleanupBlocked); Assert.True(File.Exists(reopened.PathFor(capture)));
    }

    [Fact]
    public void MissingOriginalWithRecoverySidecarRequiresConfirmation()
    {
        var repository = new CaptureRepository(root); var capture = repository.Add(Pixels, 10, 10);
        File.WriteAllText(Path.Combine(root, "history.json"), "invalid"); repository.Load(); repository.Persist();
        File.Delete(Path.Combine(root, "history.json"));
        var reopened = new CaptureRepository(root); reopened.Load(); Assert.True(reopened.CleanupBlocked);
        reopened.ConfirmHistoryRecovery();
        var final = new CaptureRepository(root); final.Load(); Assert.False(final.CleanupBlocked);
        Assert.True(File.Exists(final.PathFor(capture)));
    }

    [Fact]
    public void PromotionWriteFailurePreservesReviewedSidecarAcrossRestart()
    {
        var original = new CaptureRepository(root); var capture = original.Add(Pixels, 10, 10);
        var metadata = Path.Combine(root, "history.json"); File.WriteAllText(metadata, "unreadable original");
        var recovery = new CaptureRepository(root, (path, bytes) =>
        {
            if (path == metadata) throw new IOException("injected promotion failure");
            AtomicFile.Write(path, bytes);
        });
        recovery.Load();
        var reviewed = Assert.Single(recovery.Captures); recovery.SetPinned(reviewed, false);
        Assert.Throws<IOException>(recovery.ConfirmHistoryRecovery);
        Assert.True(recovery.CleanupBlocked);
        Assert.Equal("unreadable original", File.ReadAllText(metadata));
        Assert.Equal(0, recovery.Cleanup(DateTimeOffset.UtcNow.AddYears(1), 1, true));
        var reopened = new CaptureRepository(root); reopened.Load();
        Assert.True(reopened.CleanupBlocked);
        Assert.False(Assert.Single(reopened.Captures).Pinned);
        reopened.ConfirmHistoryRecovery(); reopened.ConfirmHistoryRecovery();
        var final = new CaptureRepository(root); final.Load();
        Assert.False(final.CleanupBlocked);
        Assert.False(Assert.Single(final.Captures).Pinned);
        Assert.True(File.Exists(final.PathFor(capture)));
        Assert.All(Directory.GetFiles(root, "history.invalid-*.json"), path => Assert.Equal("unreadable original", File.ReadAllText(path)));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
