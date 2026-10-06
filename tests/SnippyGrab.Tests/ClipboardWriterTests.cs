using System.Runtime.InteropServices;
using SnippyGrab.Core;
namespace SnippyGrab.Tests;
public sealed class ClipboardWriterTests
{
    [Fact] public async Task ExhaustionReportsFailureAndSuccessRetries()
    {
        var writer = new ClipboardWriter(); var attempts = 0;
        Assert.False(await writer.WriteAsync(() => { attempts++; throw new ExternalException(); }, _ => Task.CompletedTask)); Assert.Equal(6, attempts);
        attempts = 0; Assert.True(await writer.WriteAsync(() => { if (++attempts < 3) throw new ExternalException(); }, _ => Task.CompletedTask)); Assert.Equal(3, attempts);
    }
    [Fact] public async Task NewClipboardActionAndCancelledOcrCannotOverwrite()
    {
        var writer = new ClipboardWriter(); var writes = new List<string>();
        Assert.False(await writer.WriteAsync(() => throw new ExternalException(), async _ => { await writer.WriteAsync(() => writes.Add("new")); }));
        Assert.Single(writes);
        using var latest = new LatestOperation(); using var editor = new CancellationTokenSource();
        var old = latest.Begin(); var fresh = latest.Begin(editor.Token); Assert.True(old.IsCancellationRequested);
        editor.Cancel(); Assert.False(await writer.WriteAsync(() => writes.Add("stale OCR"), cancellation: fresh)); Assert.Single(writes);
    }
}
