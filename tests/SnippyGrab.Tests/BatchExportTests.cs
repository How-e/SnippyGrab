using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class BatchExportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SnippyGrab-batch-" + Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(20)]
    public async Task ShelfOrderAndCollisionsPreserveOriginals(int count)
    {
        var repo = new CaptureRepository(Path.Combine(root, "cache"));
        for (var i = 0; i < count; i++) repo.Add([(byte)i], 1, 1);
        var selected = repo.Captures.Reverse().ToArray(); var folder = Path.Combine(root, "export");
        var plan = BatchExport.Plan(repo, selected, folder, ExportFormat.Png, ExportCollision.Unique);
        Assert.Equal(repo.Captures, plan.Select(p => p.Capture));
        var result = await BatchExport.RunAsync(repo, plan, ExportFormat.Png, 90, ExportCollision.Unique, (_, _) => throw new Exception());
        Assert.Equal(count, result.Successful);
        foreach (var p in plan) Assert.Equal(File.ReadAllBytes(p.Source), File.ReadAllBytes(p.Destination));
        var unique = BatchExport.Plan(repo, selected, folder, ExportFormat.Png, ExportCollision.Unique); Assert.All(unique, p => Assert.False(File.Exists(p.Destination)));
        var skip = BatchExport.Plan(repo, selected, folder, ExportFormat.Png, ExportCollision.Skip);
        Assert.Equal(count, (await BatchExport.RunAsync(repo, skip, ExportFormat.Png, 90, ExportCollision.Skip, (_, _) => [])).Skipped);
    }
    [Fact]
    public async Task FailureAndCancellationKeepCompletedFiles()
    {
        var repo = new CaptureRepository(Path.Combine(root, "cache")); for (var i = 0; i < 4; i++) repo.Add([1], 1, 1);
        var plan = BatchExport.Plan(repo, repo.Captures, Path.Combine(root, "exports"), ExportFormat.Jpeg, ExportCollision.Unique);
        using var cancel = new CancellationTokenSource(); int calls = 0;
        var result = await BatchExport.RunAsync(repo, plan, ExportFormat.Jpeg, 90, ExportCollision.Unique, (_, _) =>
        { calls++; if (calls == 2) throw new IOException("injected disk full"); if (calls == 3) cancel.Cancel(); return [2]; }, cancellation: cancel.Token);
        Assert.Equal(1, result.Successful); Assert.Equal(1, result.Failed); Assert.Equal(2, result.Unprocessed);
        Assert.True(File.Exists(plan[0].Destination)); Assert.False(File.Exists(plan[1].Destination));
    }
    [Fact]
    public async Task CollidingFileAppearingAfterPlanIsNeverOverwritten()
    {
        var repo = new CaptureRepository(Path.Combine(root, "cache")); var c = repo.Add([1], 1, 1);
        var plan = BatchExport.Plan(repo, [c], root, ExportFormat.Png, ExportCollision.Unique); File.WriteAllBytes(plan[0].Destination, [9]);
        var result = await BatchExport.RunAsync(repo, plan, ExportFormat.Png, 90, ExportCollision.Unique, (_, _) => []);
        Assert.Equal(1, result.Failed); Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(plan[0].Destination)); Assert.False(c.Saved);
        Assert.Single(result.Errors); Assert.Contains(Path.GetFileName(plan[0].Destination), result.ToString());
    }
    [Fact]
    public async Task SkipPolicyCountsFilesAppearingDuringEncodingAsSkipped()
    {
        var repo = new CaptureRepository(Path.Combine(root, "cache")); var capture = repo.Add([1], 1, 1);
        var plan = BatchExport.Plan(repo, [capture], root, ExportFormat.Jpeg, ExportCollision.Skip);
        var result = await BatchExport.RunAsync(repo, plan, ExportFormat.Jpeg, 90, ExportCollision.Skip,
            (_, _) => { File.WriteAllBytes(plan[0].Destination, [9]); return [2]; });
        Assert.Equal(1, result.Skipped); Assert.Equal(0, result.Failed); Assert.Equal(0, result.Successful);
        Assert.Empty(result.Errors); Assert.False(capture.Saved);
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(plan[0].Destination));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    [Fact]
    public async Task EditingLaterItemStillExportsItsLeasedSnapshot()
    {
        var repo = new CaptureRepository(Path.Combine(root, "cache")); var later = repo.Add([2], 1, 1); repo.Add([1], 1, 1);
        var plan = BatchExport.Plan(repo, repo.Captures, root, ExportFormat.Jpeg, ExportCollision.Unique);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); using var release = new ManualResetEventSlim();
        var export = BatchExport.RunAsync(repo, plan, ExportFormat.Jpeg, 90, ExportCollision.Unique, (source, _) => { if (source == plan[0].Source) { entered.SetResult(); release.Wait(); } return File.ReadAllBytes(source); });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { repo.Replace(later, [3], 1, 1); repo.Cleanup(DateTimeOffset.UtcNow, 24, clear: true); } finally { release.Set(); }
        var result = await export; Assert.Equal(2, result.Successful); Assert.Equal(1, result.MetadataWarnings);
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(plan[1].Destination)); Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(plan[1].Source));
    }
}
