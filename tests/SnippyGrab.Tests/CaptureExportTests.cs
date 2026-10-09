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
    [Fact]
    public async Task AsyncPngPreservesBytesAndJpegUsesSharedBoundary()
    {
        var capture = repository.Add([1, 2, 3], 1, 1); var png = Path.Combine(root, "export.png"); var jpeg = Path.Combine(root, "export.jpeg");
        await CaptureExport.WriteAsync(repository, capture, png, ExportFormat.Png, 90, (_, _) => throw new Exception("PNG must not encode"));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(png));
        await CaptureExport.WriteAsync(repository, capture, jpeg, ExportFormat.Jpeg, 90, (source, quality) => { Assert.Equal(90, quality); return [255, 216, 255, 217]; });
        Assert.Equal(new byte[] { 255, 216, 255, 217 }, File.ReadAllBytes(jpeg));
        Assert.Equal(jpeg, capture.ExportPath); Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(repository.PathFor(capture)));
        await Assert.ThrowsAsync<InvalidDataException>(() => CaptureExport.WriteAsync(repository, capture, png, ExportFormat.Jpeg, 90, (_, _) => []));
        await Assert.ThrowsAsync<InvalidDataException>(() => CaptureExport.WriteAsync(repository, capture, Path.Combine(repository.Root, "export.jpg"), ExportFormat.Jpeg, 90, (_, _) => []));
    }
    [Fact]
    public async Task RevisionChangedDuringEncodingLeavesDestinationUntouched()
    {
        var capture = repository.Add([1], 1, 1); var path = Path.Combine(root, "export.jpg"); File.WriteAllBytes(path, [9]);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var export = CaptureExport.WriteAsync(repository, capture, path, ExportFormat.Jpeg, 90, (_, _) => { entered.SetResult(); release.Wait(); return [2]; });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { repository.Replace(capture, [3], 1, 1); } finally { release.Set(); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => export);
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(path)); Assert.False(capture.Saved);
    }
    [Fact]
    public async Task CancellationReleasesLeaseAndNeverWritesDestination()
    {
        var capture = repository.Add([1], 1, 1); var source = repository.PathFor(capture); var path = Path.Combine(root, "export.jpg");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(); using var cancel = new CancellationTokenSource();
        var export = CaptureExport.WriteAsync(repository, capture, path, ExportFormat.Jpeg, 90, (_, _) => { entered.SetResult(); release.Wait(); return [2]; }, cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { repository.Cleanup(DateTimeOffset.UtcNow, 24, clear: true); Assert.True(File.Exists(source)); cancel.Cancel(); } finally { release.Set(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => export);
        Assert.False(File.Exists(path)); Assert.False(capture.Saved); repository.Cleanup(DateTimeOffset.UtcNow, 24, clear: true); Assert.False(File.Exists(source));
    }
    [Fact]
    public async Task AsyncMetadataFailureStillReportsDurableJpeg()
    {
        var capture = repository.Add([1], 1, 1); var path = Path.Combine(root, "export.jpg");
        using var locked = new FileStream(Path.Combine(repository.Root, "history.json"), FileMode.Open, FileAccess.Read, FileShare.Read);
        var result = await CaptureExport.WriteAsync(repository, capture, path, ExportFormat.Jpeg, 90, (_, _) => [2]);
        Assert.False(result.MetadataSaved); Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(path)); Assert.Equal(path, capture.ExportPath);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
