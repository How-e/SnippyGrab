using System.IO;
using System.Text.Json;
using SnippyGrab.App.Services;

namespace SnippyGrab.IntegrationTests;

public sealed class LocalSessionDiagnosticsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SnippyGrab-local-diagnostics-" + Guid.NewGuid().ToString("N"));
    private static ResourceSnapshot Snapshot() => new(100, 200, 300, 10, 12, 4, 3, 2, 50, 40, 5, 1, 2, 3);
    [Fact]
    public async Task DisabledDiagnosticsNeverSampleOrCreateResourceFiles()
    {
        var reads = 0;
        using var diagnostics = new LocalSessionDiagnostics(root, action => { action(); return Task.CompletedTask; }, () => { reads++; return Snapshot(); });
        await diagnostics.SampleAsync(); diagnostics.WindowClosed(); await diagnostics.DrainAsync(); await diagnostics.StopAsync();
        Assert.Equal(0, reads); Assert.False(Directory.Exists(root));
    }
    [Fact]
    public async Task EnabledSamplesCarrySessionIdentityAndSafeResourceFieldsThenStop()
    {
        var reads = 0;
        using var diagnostics = new LocalSessionDiagnostics(root, action => { action(); return Task.CompletedTask; }, () => { reads++; return Snapshot(); });
        diagnostics.SetEnabled(true); await diagnostics.SampleAsync(); await diagnostics.DrainAsync();
        using var sample = JsonDocument.Parse(File.ReadLines(Path.Combine(root, "resources.log")).First());
        Assert.Equal(diagnostics.Session, sample.RootElement.GetProperty("Session").GetString());
        Assert.Equal(diagnostics.Build, sample.RootElement.GetProperty("Build").GetString());
        Assert.Equal(200, sample.RootElement.GetProperty("Resources").GetProperty("PrivateBytes").GetInt64());
        Assert.False(sample.RootElement.TryGetProperty("Path", out _)); Assert.False(sample.RootElement.TryGetProperty("OcrText", out _));
        diagnostics.SetEnabled(false); var before = reads; await diagnostics.SampleAsync();
        await diagnostics.StopAsync(); Assert.Equal(before, reads);
    }
    [Fact]
    public async Task BlockedDispatcherCoalescesSamplesAndStoppingSuppressesPendingWrite()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var reads = 0;
        using var diagnostics = new LocalSessionDiagnostics(root, async action => { entered.TrySetResult(); await release.Task; action(); }, () => { reads++; return Snapshot(); });
        diagnostics.SetEnabled(true); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var requests = Enumerable.Range(0, 100).Select(_ => diagnostics.SampleAsync()).ToArray();
        diagnostics.SetEnabled(false); release.SetResult(); await Task.WhenAll(requests); await diagnostics.StopAsync();
        Assert.Equal(1, reads); Assert.False(Directory.Exists(root));
    }
    [Fact]
    public async Task RepresentativeDayOfMinuteSamplesFitsTheDefaultRetentionBudget()
    {
        var reads = 0;
        using var diagnostics = new LocalSessionDiagnostics(root, action => { action(); return Task.CompletedTask; }, () => { reads++; return Snapshot(); });
        diagnostics.SetEnabled(true); await diagnostics.SampleAsync();
        for (var i = 0; i < 1440; i++) { await diagnostics.SampleAsync(); await diagnostics.DrainAsync(); }
        await diagnostics.DrainAsync(); diagnostics.SetEnabled(false); await diagnostics.StopAsync();
        var files = Directory.GetFiles(root, "resources*.log");
        Assert.InRange(reads, 1440, 1442); Assert.Equal(reads, files.Sum(file => File.ReadLines(file).Count()));
        Assert.All(files, file => Assert.InRange(new FileInfo(file).Length, 1, 1024 * 1024));
    }
    [Fact]
    public async Task ActualProcessCountersCanBeCollectedWithoutAnApplicationWindow()
    {
        using var diagnostics = new LocalSessionDiagnostics(root, action => { action(); return Task.CompletedTask; });
        diagnostics.SetEnabled(true); await diagnostics.SampleAsync(); await diagnostics.DrainAsync();
        using var sample = JsonDocument.Parse(File.ReadLines(Path.Combine(root, "resources.log")).First());
        var resources = sample.RootElement.GetProperty("Resources");
        Assert.True(resources.GetProperty("WorkingBytes").GetInt64() > 0);
        Assert.True(resources.GetProperty("PrivateBytes").GetInt64() > 0);
        Assert.True(resources.GetProperty("Handles").GetInt32() > 0);
        await diagnostics.StopAsync();
    }
    [Fact]
    public async Task RotationRetainsAtMostTwoBoundedFiles()
    {
        using var diagnostics = new LocalSessionDiagnostics(root, action => { action(); return Task.CompletedTask; }, Snapshot, maximumBytes: 2048);
        diagnostics.SetEnabled(true); await diagnostics.SampleAsync();
        for (var i = 0; i < 30; i++) await diagnostics.SampleAsync();
        await diagnostics.StopAsync(); var files = Directory.GetFiles(root, "resources*.log");
        Assert.Equal(2, files.Length);
        foreach (var file in files)
        {
            Assert.InRange(new FileInfo(file).Length, 1, 2048);
            foreach (var line in File.ReadLines(file)) { using var parsed = JsonDocument.Parse(line); Assert.Equal(diagnostics.Session, parsed.RootElement.GetProperty("Session").GetString()); }
        }
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
