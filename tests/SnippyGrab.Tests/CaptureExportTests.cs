using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class CaptureExportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SnippyGrab-export-test-" + Guid.NewGuid().ToString("N"));
    private readonly CaptureRepository repository;
    public CaptureExportTests() => repository = new(Path.Combine(root, "cache"));
    [Fact]
    public void ExportAndRepeatWriteCurrentRevisionAndRememberDestination()
    {
        var capture = repository.Add([1, 2], 2, 1); var path = Path.Combine(root, "exports", "output.png");
        Assert.True(CaptureExport.Write(repository, capture, path).MetadataSaved); Assert.Equal(new byte[] { 1, 2 }, File.ReadAllBytes(path));
        repository.Replace(capture, [3, 4], 2, 1); CaptureExport.Write(repository, capture, path);
        Assert.Equal(new byte[] { 3, 4 }, File.ReadAllBytes(path));
        var reopened = new CaptureRepository(repository.Root); reopened.Load();
        Assert.True(reopened.Captures[0].Saved); Assert.Equal(path, reopened.Captures[0].ExportPath);
    }
    [Fact]
    public void ManagedCacheAndMisleadingExtensionsAreRejectedWithoutSuccessState()
    {
        var capture = repository.Add([1], 1, 1);
        Assert.Throws<InvalidDataException>(() => CaptureExport.Write(repository, capture, repository.PathFor(capture)));
        var trailing = new CaptureRepository(repository.Root + Path.DirectorySeparatorChar);
        Assert.Throws<InvalidDataException>(() => CaptureExport.Write(trailing, capture, trailing.PathFor(capture)));
        Assert.Throws<InvalidDataException>(() => CaptureExport.Write(repository, capture, Path.Combine(root, "fake.jpg")));
        Assert.False(capture.Saved); Assert.Equal("", capture.ExportPath);
    }
    [Fact]
    public void DestinationConflictDoesNotMarkCaptureSavedOrDamageSource()
    {
        var capture = repository.Add([1], 1, 1); var path = Path.Combine(root, "directory.png"); Directory.CreateDirectory(path);
        var error = Record.Exception(() => CaptureExport.Write(repository, capture, path));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.False(capture.Saved); Assert.Equal("", capture.ExportPath); Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(repository.PathFor(capture)));
    }
    [Fact]
    public void MetadataFailureReportsSuccessfulFileExportWithWarning()
    {
        var capture = repository.Add([1, 2, 3], 3, 1); var path = Path.Combine(root, "export.png");
        using var locked = new FileStream(Path.Combine(repository.Root, "history.json"), FileMode.Open, FileAccess.Read, FileShare.Read);
        var result = CaptureExport.Write(repository, capture, path);
        Assert.False(result.MetadataSaved); Assert.True(capture.Saved); Assert.Equal(path, result.Path);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void StaleRecordCannotOverwriteAnExistingExport()
    {
        var capture = repository.Add([1], 1, 1);
        var stale = new CaptureRecord { Id = capture.Id, FileName = capture.FileName, Width = 1, Height = 1 };
        repository.Replace(capture, [2], 1, 1);
        var path = Path.Combine(root, "output.png"); File.WriteAllBytes(path, [9]);
        Assert.Throws<InvalidOperationException>(() => CaptureExport.Write(repository, stale, path));
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(path));
        Assert.False(stale.Saved); Assert.False(capture.Saved);
        CaptureExport.Write(repository, capture, path);
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void FailedRepeatExportPreservesPreviousDestinationAndPixels()
    {
        var capture = repository.Add([1], 1, 1); var path = Path.Combine(root, "output.png");
        CaptureExport.Write(repository, capture, path); repository.Replace(capture, [2], 1, 1);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failure = Record.Exception(() => CaptureExport.Write(repository, capture, path));
            Assert.True(failure is IOException or UnauthorizedAccessException);
        }
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(path));
        Assert.True(capture.Saved); Assert.Equal(path, capture.ExportPath);
        CaptureExport.Write(repository, capture, path);
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(path));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
