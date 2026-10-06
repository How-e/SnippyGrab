using System.Text.Json;
using System.Buffers.Binary;
using SnippyGrab.Core;
namespace SnippyGrab.Tests;
public sealed class HistoryPagingTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SnippyGrab-pages-" + Guid.NewGuid().ToString("N"));
    [Fact] public void CorruptPageFailsClosedAndPreservesPossiblePins()
    {
        var repository = new CaptureRepository(root);
        for (var i = 0; i < 513; i++) File.WriteAllBytes(Path.Combine(root, "capture-" + Guid.NewGuid().ToString("N") + ".png"), [1]);
        repository.Load(); repository.Persist();
        File.WriteAllText(Directory.GetFiles(root, "history-page-*.json")[0], "[]");
        var reopened = new CaptureRepository(root); reopened.Load(); Assert.True(reopened.CleanupBlocked);
        Assert.Equal(513, reopened.Captures.Count); Assert.All(reopened.Captures, capture => Assert.True(capture.Pinned));
        Assert.Equal(0, reopened.Cleanup(DateTimeOffset.UtcNow, 1, true));
    }
    [Fact] public void ThousandsOfOrphansPersistAsBoundedPagesWithoutLosingPins()
    {
        var repository = new CaptureRepository(root);
        for (var i = 0; i < 2100; i++) File.WriteAllBytes(Path.Combine(root, "capture-" + Guid.NewGuid().ToString("N") + ".png"), [1]);
        repository.Load(); Assert.Equal(2100, repository.Captures.Count);
        repository.Captures[0].Pinned = true; var pin = repository.Captures[0].Id;
        // Oversized aggregate metadata still fits in bounded pages.
        foreach (var capture in repository.Captures) capture.ExportPath = new string('a', 2100);
        repository.Persist();
        Assert.True(new FileInfo(Path.Combine(root, "history.json")).Length < 4 * 1024 * 1024);
        Assert.Equal(2, JsonSerializer.Deserialize<RepositoryState>(File.ReadAllText(Path.Combine(root, "history.json")))!.SchemaVersion);
        var reopened = new CaptureRepository(root); reopened.Load(); Assert.False(reopened.CleanupBlocked); Assert.Equal(2100, reopened.Captures.Count);
        Assert.Contains(reopened.Captures, c => c.Id == pin && c.Pinned);
        Assert.Equal(0, reopened.Cleanup(DateTimeOffset.UtcNow.AddYears(1), -1));
        Assert.All(Directory.GetFiles(root, "history-page-*.json"), path => Assert.True(new FileInfo(path).Length <= 4 * 1024 * 1024));
    }
    [Fact] public void LazyDimensionsReadPngHeaderAndRejectInvalidBounds()
    {
        var repository = new CaptureRepository(root); var header = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(header, 0); "IHDR"u8.CopyTo(header.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16), 1920); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(20), 1080);
        var path = Path.Combine(root, "capture-" + Guid.NewGuid().ToString("N") + ".png"); File.WriteAllBytes(path, header);
        repository.Load(); var record = Assert.Single(repository.Captures); Assert.True(record.DimensionsPending);
        repository.ResolveDimensions(record); Assert.Equal(1920, record.Width); Assert.Equal(1080, record.Height); Assert.False(record.DimensionsPending);
        record.DimensionsPending = true; BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16), int.MaxValue); File.WriteAllBytes(path, header);
        Assert.Throws<InvalidDataException>(() => repository.ResolveDimensions(record));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
