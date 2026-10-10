using System.Runtime.InteropServices;
using SnippyGrab.Core;
namespace SnippyGrab.Tests;

public sealed class ClipboardWriterTests
{
    [Fact]
    public async Task PreparationReservedBeforeYieldCannotOverwriteNewerAction()
    {
        var writer = new ClipboardWriter(); var old = writer.BeginOperation(); var writes = new List<string>();
        Assert.True(await writer.WriteAsync(() => writes.Add("new")));
        Assert.False(await writer.WriteAsync(() => writes.Add("old prepared image"), reservedOperation: old));
        Assert.Equal(new[] { "new" }, writes);
        var pending = writer.BeginOperation(); writer.Invalidate();
        Assert.False(await writer.WriteAsync(() => writes.Add("after exit"), reservedOperation: pending));
    }
    [Fact]
    public async Task ExhaustionReportsFailureAndSuccessRetries()
    {
        var writer = new ClipboardWriter(); var attempts = 0;
        Assert.False(await writer.WriteAsync(() => { attempts++; throw new ExternalException(); }, _ => Task.CompletedTask)); Assert.Equal(6, attempts);
        attempts = 0; Assert.True(await writer.WriteAsync(() => { if (++attempts < 3) throw new ExternalException(); }, _ => Task.CompletedTask)); Assert.Equal(3, attempts);
    }
    [Fact]
    public async Task NewClipboardActionAndCancelledOcrCannotOverwrite()
    {
        var writer = new ClipboardWriter(); var writes = new List<string>();
        Assert.False(await writer.WriteAsync(() => throw new ExternalException(), async _ => { await writer.WriteAsync(() => writes.Add("new")); }));
        Assert.Single(writes);
        using var latest = new LatestOperation(); using var editor = new CancellationTokenSource();
        var old = latest.Begin(); var fresh = latest.Begin(editor.Token); Assert.True(old.IsCancellationRequested);
        editor.Cancel(); Assert.False(await writer.WriteAsync(() => writes.Add("stale OCR"), cancellation: fresh)); Assert.Single(writes);
    }
    [Fact]
    public async Task CancellationDuringRetryDelayPreventsLatePublication()
    {
        var writer = new ClipboardWriter(); var attempts = 0;
        using var lifetime = new CancellationTokenSource();
        Assert.False(await writer.WriteAsync(() => { attempts++; throw new ExternalException(); }, _ => { lifetime.Cancel(); return Task.CompletedTask; }, lifetime.Token));
        Assert.Equal(1, attempts);
        Assert.True(await writer.WriteAsync(() => attempts++));
        Assert.Equal(2, attempts);
    }
}
