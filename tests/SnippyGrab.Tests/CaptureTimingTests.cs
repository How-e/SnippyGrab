using System.Text.Json;
using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class CaptureTimingTests
{
    [Fact]
    public void SelectionToClipboardExcludesHumanSelectionAndSplitsQueueFromEncoding()
    {
        double ms = 0; var timing = new CaptureTiming(CaptureMode.Region, () => ms); timing.Start();
        ms = 200; timing.OverlayShown(); ms = 5200; timing.SelectionCompleted();
        var queue = timing.Measure(CapturePhase.EncodingQueue); ms = 5250; queue.Dispose(); queue.Dispose();
        using (timing.Measure(CapturePhase.Encoding)) ms = 5270;
        using (timing.Measure(CapturePhase.Storage)) ms = 5290;
        timing.Stored(true); using (timing.Measure(CapturePhase.Clipboard)) ms = 5300;
        timing.ClipboardPublished(true); timing.Complete(CaptureOutcome.Completed); var result = timing.Finish();
        Assert.Equal(5300, result.InteractiveTotalMs); Assert.Equal(200, result.RequestToOverlayMs);
        Assert.Equal(100, result.SelectionToClipboardMs); Assert.Equal(90, result.SelectionToStoredMs);
        Assert.Equal(50, result.PhaseMilliseconds["EncodingQueue"]); Assert.Equal(20, result.PhaseMilliseconds["Encoding"]);
        Assert.True(result.ClipboardCopied); Assert.True(result.HistorySaved);
    }
    [Fact]
    public void FailedClipboardAndNoninteractiveCaptureDoNotInventSelectionLatency()
    {
        double ms = 0; var timing = new CaptureTiming(CaptureMode.Desktop, () => ms); timing.Start();
        ms = 50; timing.Stored(false); timing.ClipboardPublished(false); timing.Complete(CaptureOutcome.ClipboardUnavailable);
        var result = timing.Finish(); Assert.Null(result.SelectionToClipboardMs); Assert.Null(result.SelectionToStoredMs);
        Assert.False(result.ClipboardCopied); Assert.False(result.HistorySaved); Assert.Null(result.RequestToOverlayMs);
        Assert.Equal(CaptureOutcome.ClipboardUnavailable, result.Status);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailureAndCancellationAreCorrelatedWithoutPrivateExceptionContent(bool cancel)
    {
        var timing = new CaptureTiming(CaptureMode.Window); timing.Start();
        timing.Fail(cancel ? new OperationCanceledException("private OCR text") : new IOException("private file path"));
        var result = timing.Finish(); Assert.Equal(timing.Operation, result.Operation);
        Assert.Equal(cancel ? CaptureOutcome.Cancelled : CaptureOutcome.Failed, result.Status);
        Assert.DoesNotContain("private", JsonSerializer.Serialize(result)); Assert.NotNull(result.ExceptionType);
    }
    [Fact]
    public void AbandonedOperationIsCancelledAndFinishedSnapshotIsStable()
    {
        double ms = 0; var timing = new CaptureTiming(CaptureMode.Region, () => ms); timing.Start(); ms = 100;
        var result = timing.Finish(); ms = 1000;
        Assert.Equal(CaptureOutcome.Cancelled, result.Status); Assert.Same(result, timing.Finish());
        Assert.Equal(100, result.InteractiveTotalMs); Assert.Null(result.SelectionToClipboardMs);
    }
}
