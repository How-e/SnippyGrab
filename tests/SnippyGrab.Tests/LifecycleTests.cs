using System.Text.Json;
using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class LifecycleTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SnippyGrab-tests-" + Guid.NewGuid().ToString("N"));
    private readonly CaptureRepository repository;
    // Synthetic marker bytes: repository tests exercise storage, not the OS image codec.
    private static readonly byte[] Synthetic = [137, 80, 78, 71, 13, 10, 26, 10];
    public LifecycleTests() => repository = new(root);
    private CaptureRecord Add() => repository.Add(Synthetic, 640, 480);
    [Theory]
    [InlineData("../capture-a.png")]
    [InlineData("C:\\private.png")]
    [InlineData("capture-ABC.png")]
    [InlineData("capture-123.png")]
    [InlineData("image.png")]
    [InlineData("capture-00000000000000000000000000000000.png:stream")]
    [InlineData(null)]
    public void RejectsUnsafeNames(string? name) => Assert.False(CaptureRepository.IsSafeName(name));
    [Fact]
    public void UsesUniqueSafeNames()
    { var a = Add(); var b = Add(); Assert.True(CaptureRepository.IsSafeName(a.FileName)); Assert.NotEqual(a.FileName, b.FileName); Assert.StartsWith(root, repository.PathFor(a)); }
    [Fact] public void RejectsPathTraversal() => Assert.Throws<InvalidDataException>(() => repository.PathForName("../other.png"));
    [Fact]
    public void RetentionKeepsRecentAndRemovesExpired()
    { var a = Add(); Assert.Equal(0, repository.Cleanup(a.CreatedUtc.AddMinutes(59), 1)); Assert.Equal(1, repository.Cleanup(a.CreatedUtc.AddHours(1), 1)); Assert.Empty(repository.Captures); }
    [Fact]
    public void NeverRetentionKeepsOldCaptures()
    { var a = Add(); Assert.Equal(0, repository.Cleanup(a.CreatedUtc.AddYears(10), -1)); }
    [Fact]
    public void PinsWinOverRetentionAndExplicitClear()
    { var a = Add(); a.Pinned = true; repository.Persist(); Assert.Equal(0, repository.Cleanup(a.CreatedUtc.AddYears(1), 1, true)); Assert.True(File.Exists(repository.PathFor(a))); }
    [Fact]
    public void EditorLeasePreventsClear()
    { var a = Add(); using (repository.Lease([a])) Assert.Equal(0, repository.Cleanup(DateTimeOffset.UtcNow, 1, true)); Assert.Equal(1, repository.Cleanup(DateTimeOffset.UtcNow, 1, true)); }
    [Fact]
    public void NestedLeaseAndDoubleDisposeAreSafe()
    { var a = Add(); var first = repository.Lease([a]); var second = repository.Lease([a]); first.Dispose(); first.Dispose(); Assert.Equal(0, repository.Cleanup(DateTimeOffset.UtcNow, 1, true)); second.Dispose(); Assert.Equal(1, repository.Cleanup(DateTimeOffset.UtcNow, 1, true)); }
    [Fact]
    public void TransferGraceSurvivesRestart()
    { var a = Add(); using (repository.Lease([a], true)) Assert.Equal(0, repository.Cleanup(DateTimeOffset.UtcNow, 1, true)); var reopened = new CaptureRepository(root); reopened.Load(); Assert.Equal(0, reopened.Cleanup(DateTimeOffset.UtcNow.AddHours(23), 1, true)); Assert.Equal(1, reopened.Cleanup(DateTimeOffset.UtcNow.AddHours(25), 1, true)); }
    [Fact]
    public void ReplacingImageDoesNotOverwriteTransferSource()
    { var a = Add(); var path = repository.PathFor(a); using var lease = repository.Lease([a], true); repository.Replace(a, [1, 2, 3], 100, 200); Assert.NotEqual(path, repository.PathFor(a)); Assert.Equal(Synthetic, File.ReadAllBytes(path)); Assert.True(a.Edited); Assert.Equal(100, a.Width); }
    [Fact]
    public void HistoryRoundTripsPinsAndEdits()
    { var a = Add(); a.Pinned = true; a.Edited = true; a.Saved = true; repository.Persist(); var copy = new CaptureRepository(root); copy.Load(); var record = Assert.Single(copy.Captures); Assert.True(record.Pinned && record.Edited && record.Saved); Assert.Equal(a.Id, record.Id); }
    [Fact]
    public void FailedEditorMetadataCommitRetainsOriginalRecordAndLease()
    {
        var record = Add(); var original = record.FileName;
        using var lease = repository.Lease([record]);
        var metadata = Path.Combine(root, "history.json");
        using (var locked = new FileStream(metadata, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failure = Record.Exception(() => repository.Replace(record, [1, 2, 3], 100, 200));
            Assert.True(failure is IOException or UnauthorizedAccessException);
            Assert.Equal(original, record.FileName); Assert.Equal(640, record.Width); Assert.False(record.Edited);
            Assert.Equal(Synthetic, File.ReadAllBytes(repository.PathFor(record)));
        }
        repository.Replace(record, [1, 2, 3], 100, 200);
        var reopened = new CaptureRepository(root); reopened.Load();
        Assert.Contains(reopened.Captures, c => c.Id == record.Id && c.FileName == record.FileName && c.Edited);
        repository.Cleanup(DateTimeOffset.UtcNow, 1, true);
        Assert.True(File.Exists(Path.Combine(root, original)));
    }
    [Fact]
    public void OptionalHistoryStillPersistsPins()
    { var a = Add(); var b = Add(); a.Pinned = true; repository.HistoryEnabled = false; repository.Persist(); var copy = new CaptureRepository(root) { HistoryEnabled = false }; copy.Load(); Assert.Equal(a.Id, Assert.Single(copy.Captures).Id); Assert.True(File.Exists(repository.PathFor(b))); }
    [Fact]
    public void RecoversOwnedOrphansAfterCrash()
    { var name = "capture-" + Guid.NewGuid().ToString("N") + ".png"; File.WriteAllBytes(Path.Combine(root, name), Synthetic); repository.Load(); Assert.Equal(name, Assert.Single(repository.Captures).FileName); Assert.True(repository.Captures[0].Dismissed); }
    [Fact]
    public void CorruptHistoryFailsClosedForPinSafety()
    { var a = Add(); a.Pinned = true; File.WriteAllText(Path.Combine(root, "history.json"), "{invalid"); repository.Load(); Assert.True(repository.CleanupBlocked); Assert.Equal(0, repository.Cleanup(DateTimeOffset.UtcNow.AddYears(1), 1, true)); Assert.True(File.Exists(repository.PathFor(a))); repository.Persist(); Assert.Equal("{invalid", File.ReadAllText(Path.Combine(root, "history.json"))); }
    [Fact]
    public void IgnoresUnknownFiles()
    { File.WriteAllText(Path.Combine(root, "important.png"), "keep"); File.WriteAllText(Path.Combine(root, "capture-nope.png"), "keep"); Assert.Equal(0, repository.Cleanup(DateTimeOffset.UtcNow, 1, true)); Assert.Equal(2, Directory.EnumerateFiles(root, "*.png").Count()); }
    [Fact]
    public void CleanupOnlyDeletesOwnedIncompleteWrites()
    {
        var unknown = Path.Combine(root, "capture-family.tmp");
        File.WriteAllText(unknown, "keep"); File.SetLastWriteTimeUtc(unknown, DateTime.UtcNow.AddDays(-5));
        var owned = Path.Combine(root, "capture-" + Guid.NewGuid().ToString("N") + ".png." + Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllText(owned, "incomplete"); File.SetLastWriteTimeUtc(owned, DateTime.UtcNow.AddDays(-5));
        repository.Cleanup(DateTimeOffset.UtcNow, 24);
        Assert.True(File.Exists(unknown)); Assert.False(File.Exists(owned));
    }
    [Fact]
    public void RejectsTrailingNewlineInFilename() => Assert.False(CaptureRepository.IsSafeName("capture-00000000000000000000000000000000.png\n"));
    [Fact]
    public void TransferPayloadIncludesMultipleRealPaths()
    { var a = Add(); var b = Add(); var paths = TransferPayload.Files(repository, [a, b, a]); Assert.Equal(2, paths.Length); Assert.All(paths, p => Assert.True(File.Exists(p))); }
    [Fact]
    public void MissingTransferFileIsAnError()
    { var a = Add(); File.Delete(repository.PathFor(a)); Assert.Throws<IOException>(() => TransferPayload.Files(repository, [a])); }
    [Fact]
    public void SettingsMigrateVersionZero()
    { var service = new SettingsService(Path.Combine(root, "settings.json")); File.WriteAllText(Path.Combine(root, "settings.json"), "{\"SchemaVersion\":0,\"ThumbnailSize\":300}"); var settings = service.Load(); Assert.Equal(1, settings.SchemaVersion); Assert.Equal(300, settings.ThumbnailSize); }
    [Fact]
    public void SettingsRoundTrip()
    { var service = new SettingsService(Path.Combine(root, "settings.json")); var settings = new Settings { Corner = DockCorner.TopLeft, PrimaryHotkey = new(70, 6), HistoryEnabled = false }; service.Save(settings); var copy = service.Load(); Assert.Equal(DockCorner.TopLeft, copy.Corner); Assert.Equal(settings.PrimaryHotkey, copy.PrimaryHotkey); Assert.False(copy.HistoryEnabled); }
    [Fact]
    public void SettingsClampUnsafeSizes()
    { var s = new Settings { ThumbnailSize = 99999, ExpandedItems = 99, DockOpacity = double.NaN, StrokeSize = double.PositiveInfinity, CleanupMinutes = -10 }; s.Validate(); Assert.Equal(400, s.ThumbnailSize); Assert.Equal(5, s.ExpandedItems); Assert.Equal(0.96, s.DockOpacity); Assert.Equal(3, s.StrokeSize); Assert.Equal(1, s.CleanupMinutes); }
    [Fact]
    public void FutureSettingsAreQuarantined()
    { var file = Path.Combine(root, "settings.json"); File.WriteAllText(file, "{\"SchemaVersion\":999}"); var service = new SettingsService(file); Assert.Equal(1, service.Load().SchemaVersion); Assert.True(service.Recovered); Assert.Single(Directory.GetFiles(root, "settings.json.invalid-*")); }
    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"PrimaryHotkey\":null}")]
    public void InvalidSettingsRecover(string contents)
    { var file = Path.Combine(root, "settings.json"); File.WriteAllText(file, contents); var service = new SettingsService(file); Assert.NotNull(service.Load().PrimaryHotkey); Assert.True(service.Recovered); }
    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(168)]
    [InlineData(192)]
    public void DpiRoundTripsPhysicalCoordinates(int dpi)
    { foreach (var pixels in new[] { -1920, -221, 0, 2560, 4480 }) Assert.Equal(pixels, DpiGeometry.ToPixel(DpiGeometry.ToDip(pixels, dpi), dpi)); }
    [Fact]
    public void RegionsUseNegativeDesktopOrigin()
    { var desktop = new PixelRect(-1920, -221, 6400, 1821); var selection = PixelRect.Between(-1000, 600, -1700, -100); Assert.Equal(new PixelRect(-1700, -100, 700, 700), selection); Assert.Equal(new PixelRect(220, 121, 700, 700), selection.RelativeTo(desktop)); }
    [Fact]
    public void ClipRegionToDesktopBounds()
    { Assert.Equal(new PixelRect(-1920, 0, 1920, 1080), new PixelRect(-3000, 0, 3000, 3000).Intersect(new(-1920, 0, 1920, 1080))); }
    [Fact]
    public void UndoRedoBranchesAndBounds()
    { var journal = new UndoJournal<int>(0, 3); journal.Push(1); journal.Push(2); Assert.Equal(1, journal.Undo()); journal.Push(3); Assert.False(journal.CanRedo); journal.Push(4); Assert.Equal(3, journal.Undo()); Assert.Equal(1, journal.Undo()); Assert.Equal(1, journal.Undo()); Assert.Equal(3, journal.Redo()); }
    [Fact]
    public void StartupPathIsQuoted()
    { Assert.Equal("\"C:\\Program Files\\SnippyGrab\\SnippyGrab.exe\" --background", StartupCommand.Build(@"C:\Program Files\SnippyGrab\SnippyGrab.exe")); }
    [Theory]
    [InlineData("cmd.exe")]
    [InlineData("C:\\x\" & evil.exe")]
    [InlineData("C:\\file.txt")]
    public void RejectsUnsafeStartupCommand(string path) => Assert.Throws<ArgumentException>(() => StartupCommand.Build(path));
    [Fact] public void HotkeyNamesAreReadable() { Assert.Equal("Ctrl+Shift+F8", new Hotkey(119, 6).ToString()); Assert.Equal("Alt+PrintScreen", new Hotkey(44, 1).ToString()); }
    [Fact]
    public void ShelfExpirationRespectsPinsAndDismissal()
    {
        var capture = Add(); var now = capture.CreatedUtc.AddMinutes(31);
        Assert.False(CaptureLifetime.Visible(capture, 30, now));
        Assert.True(CaptureLifetime.Visible(capture, 0, now));
        capture.Pinned = true; Assert.True(CaptureLifetime.Visible(capture, 30, now));
        capture.Dismissed = true; Assert.False(CaptureLifetime.Visible(capture, 0, now));
    }
    [Fact]
    public void OptionalHotkeysCanBeDisabled()
    {
        var settings = new Settings { FallbackHotkey = new(0, 0) }; settings.Validate();
        Assert.Equal("Disabled", settings.FallbackHotkey.ToString());
    }
    [Fact]
    public void NullHistoryEntriesBlockDestructiveCleanup()
    {
        Add(); File.WriteAllText(Path.Combine(root, "history.json"), "{\"SchemaVersion\":1,\"Captures\":[null]}");
        repository.Load(); Assert.True(repository.CleanupBlocked);
    }
    [Fact]
    public void NullableSettingsStringsRecoverWithoutCrashing()
    {
        var file = Path.Combine(root, "settings.json"); File.WriteAllText(file, "{\"CachePath\":null,\"SaveDirectory\":null,\"AnnotationColor\":null}");
        var settings = new SettingsService(file).Load(); Assert.Equal("", settings.CachePath); Assert.NotNull(settings.SaveDirectory); Assert.NotNull(settings.AnnotationColor);
    }
    [Fact]
    public void RestoringExpiredCaptureStartsNewLifetimeAndKeepsOriginalTimestamp()
    {
        var record = Add(); var original = record.CreatedUtc; record.Dismissed = true;
        var restored = original.AddDays(3); repository.Restore([record], restored);
        Assert.Equal(original, record.CreatedUtc); Assert.True(CaptureLifetime.Visible(record, 30, restored.AddMinutes(29)));
        Assert.False(CaptureLifetime.Visible(record, 30, restored.AddMinutes(30)));
        Assert.Equal(0, repository.Cleanup(restored.AddMinutes(29), 1));
        var reopened = new CaptureRepository(root); reopened.Load(); Assert.Equal(restored, Assert.Single(reopened.Captures).RestoredUtc);
    }
    [Fact]
    public void StaleEditorCannotOverwriteCommittedRevisionOrEmitRefresh()
    {
        var record = Add(); var expected = record.FileName; var events = 0;
        repository.RevisionChanged += _ => events++;
        repository.Replace(record, [2], 40, 50, expected); var latest = record.FileName;
        Assert.Throws<InvalidOperationException>(() => repository.Replace(record, [3], 60, 70, expected));
        Assert.Equal(latest, record.FileName); Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(repository.PathFor(record))); Assert.Equal(1, events);
    }
    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(root)) File.Delete(file);
        Directory.Delete(root);
    }
}
