using System.Security.Cryptography;
using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class StreamingExportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SnippyGrab-stream-export-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task LargePngStreamsExactStoredBytesWithoutCallingEncoder()
    {
        var repository = new CaptureRepository(Path.Combine(root, "cache"));
        var bytes = new byte[12 * 1024 * 1024]; Random.Shared.NextBytes(bytes);
        var record = await repository.AddAsync(bytes, 1, 1);
        var destination = Path.Combine(root, "export.png");
        var result = await CaptureExport.WriteAsync(repository, record, destination, ExportFormat.Png, 90,
            (_, _) => throw new InvalidOperationException("PNG must not encode."));
        using var output = File.OpenRead(destination);
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(output)); Assert.True(result.MetadataSaved);
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateCollisionRespectsOverwriteDecision(bool overwrite)
    {
        Directory.CreateDirectory(root); var destination = Path.Combine(root, "export.png");
        var write = AtomicFile.WriteCheckedAsync(destination, (output, token) => output.WriteAsync(new byte[] { 2 }, token).AsTask(),
            overwrite, () => { }, () => File.WriteAllBytes(destination, [9]), default);
        if (overwrite) { await write; Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(destination)); }
        else { await Assert.ThrowsAsync<IOException>(() => write); Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(destination)); }
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationOrStaleRevisionBeforeCommitLeavesOriginalIntact(bool cancel)
    {
        Directory.CreateDirectory(root); var destination = Path.Combine(root, "export.png"); File.WriteAllBytes(destination, [9]);
        using var lifetime = new CancellationTokenSource();
        var write = AtomicFile.WriteCheckedAsync(destination, (output, token) => output.WriteAsync(new byte[] { 2 }, token).AsTask(),
            true, () => { }, () => { if (cancel) lifetime.Cancel(); else throw new InvalidOperationException("Stale revision"); }, lifetime.Token);
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write);
        else await Assert.ThrowsAsync<InvalidOperationException>(() => write);
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(destination)); Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
