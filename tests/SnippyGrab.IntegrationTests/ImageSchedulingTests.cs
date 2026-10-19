using System.Buffers.Binary;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnippyGrab.App.Services;
using SnippyGrab.Core;

namespace SnippyGrab.IntegrationTests;

public sealed class ImageSchedulingTests
{
    [Fact]
    public async Task CopyBurstRunsOnlyFirstAndLatestPreparationAndTextCancelsQueuedCopies()
    {
        var image = ImageIntegrationTests.Sta(() => { var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8); bitmap.Freeze(); return bitmap; });
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim(); var calls = new List<string>(); var writes = 0;
        var service = new ClipboardService(_ => writes++, prepareStored: (path, _, _) =>
        {
            lock (calls) calls.Add(path);
            if (path == "first") { entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); }
            return (image, null);
        });
        var first = service.StoredImageAsync("first", false); Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        var queued = Enumerable.Range(0, 25).Select(i => service.StoredImageAsync("queued-" + i, false)).ToArray();
        release.Set(); Assert.False(await first); var results = await Task.WhenAll(queued);
        Assert.Equal(1, results.Count(result => result)); Assert.Equal(1, writes); Assert.Equal(new[] { "first", "queued-24" }, calls);
        entered.Reset(); release.Reset(); calls.Clear(); writes = 0;
        first = service.StoredImageAsync("first", false); Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        var old = service.StoredImageAsync("obsolete", false); Assert.True(await service.TextAsync("new text")); release.Set();
        Assert.False(await first); Assert.False(await old); Assert.Equal(new[] { "first" }, calls); Assert.Equal(1, writes);
    }
    [Fact]
    public async Task ImagingReusesOneStaThreadAndReturnsFrozenCrossThreadResults()
    {
        var threads = new HashSet<int>();
        for (var i = 0; i < 50; i++)
        {
            var result = await ImageWorker.RunAsync(() =>
            {
                Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
                var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8); bitmap.Freeze(); return (Thread.CurrentThread.ManagedThreadId, bitmap);
            });
            threads.Add(result.ManagedThreadId); Assert.True(result.bitmap.IsFrozen);
        }
        Assert.Single(threads);
    }
    [Fact]
    public async Task CaptureWorkCompletesWhileBackgroundImagingIsBlockedAndReusesItsSta()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var background = ImageWorker.RunAsync(() => { entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); return Thread.CurrentThread.ManagedThreadId; });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        int captureThread;
        try
        {
            captureThread = await ImageWorker.RunCaptureAsync(() =>
            {
                Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
                var image = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8); image.Freeze();
                return Thread.CurrentThread.ManagedThreadId;
            }).WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(background.IsCompleted);
            for (var i = 0; i < 20; i++) Assert.Equal(captureThread, await ImageWorker.RunCaptureAsync(() => Thread.CurrentThread.ManagedThreadId));
        }
        finally { release.Set(); }
        Assert.NotEqual(captureThread, await background);
    }
    [Fact]
    public async Task LongOcrWorkDoesNotBlockCopiesPreviewsOrCaptureEncodingAndReusesItsSta()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var recognition = ImageWorker.RunOcrAsync(() =>
        {
            Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
            entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return Thread.CurrentThread.ManagedThreadId;
        });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        int imageThread;
        try
        {
            imageThread = await ImageWorker.RunAsync(() =>
            {
                var image = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8); image.Freeze();
                Assert.NotEmpty(ImageService.Png(image)); return Thread.CurrentThread.ManagedThreadId;
            }).WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(recognition.IsCompleted);
            using var cancelled = new CancellationTokenSource();
            var obsolete = ImageWorker.RunOcrAsync<int>(() => throw new InvalidOperationException("Cancelled OCR must not run."), cancelled.Token);
            cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => obsolete);
        }
        finally { release.Set(); }
        var ocrThread = await recognition; Assert.NotEqual(imageThread, ocrThread);
        for (var i = 0; i < 20; i++) Assert.Equal(ocrThread, await ImageWorker.RunOcrAsync(() => Thread.CurrentThread.ManagedThreadId));
    }
    [Fact]
    public void OcrRejectsOversizedPngHeaderBeforeDecodingOrNativeAllocation()
    {
        var bytes = ImageIntegrationTests.Sta(() => ImageService.Png(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8)));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), 16_000_001); BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), 1);
        Assert.Contains("16 megapixels", Assert.Throws<InvalidDataException>(() => OcrService.DecodePixels(bytes)).Message);
    }
}
