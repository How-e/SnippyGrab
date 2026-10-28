using System.Globalization;
using System.Text.Json;

namespace SnippyGrab.App.Services;

internal static class OcrSchedulingChecks
{
    internal static async Task Run(string destination)
    {
        var image = await ImageWorker.RunAsync(() =>
        {
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, 1200, 300));
                drawing.DrawText(new FormattedText("Error CS1002: semicolon expected", CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Consolas"), 32, Brushes.Black, 1), new Point(30, 70));
            }
            var bitmap = new RenderTargetBitmap(1200, 300, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual); bitmap.Freeze(); return bitmap;
        });
        var png = await ImageWorker.RunAsync(() => ImageService.Png(image));
        using var service = new OcrService();
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var occupied = ImageWorker.RunOcrAsync(() =>
        {
            entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("OCR scheduling fixture timed out.");
            return Thread.CurrentThread.ManagedThreadId;
        });
        Task<string>? recognition = null; var copies = 0;
        try
        {
            if (!entered.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("OCR worker did not start.");
            recognition = service.ReadAsync(png);
            await ImageWorker.RunAsync(() => ImageService.Png(image)).WaitAsync(TimeSpan.FromSeconds(10));
            var clipboard = new ClipboardService(_ => copies++);
            if (!await clipboard.ImageAsync(image, true).WaitAsync(TimeSpan.FromSeconds(10))) throw new InvalidOperationException("Synthetic copy failed.");
            if (recognition.IsCompleted || service.EngineCreations != 0)
                throw new InvalidOperationException("OCR recognition did not remain queued on its isolated worker.");
        }
        finally { release.Set(); await occupied; }
        if (recognition is null || !(await recognition.WaitAsync(TimeSpan.FromSeconds(30))).Contains("CS1002", StringComparison.Ordinal))
            throw new InvalidOperationException("Native OCR fixture did not recognize CS1002.");
        if (!(await service.ReadAsync(png)).Contains("CS1002", StringComparison.Ordinal) || service.EngineCreations != 1)
            throw new InvalidOperationException("OCR engine was not reused.");
        AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(new
        {
            Result = "PASS",
            Build = BuildVersion.Display,
            OcrIsolation = true,
            CopiesDuringBlockedOcr = copies,
            NativeRecognition = true,
            EngineCreations = service.EngineCreations,
            Scope = "Packaged production OCR queues on its dedicated STA while image work and an intercepted copy complete. Real synthetic CS1002 recognition and engine reuse follow release. No desktop capture or OS clipboard writes; not all-day resource acceptance."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
