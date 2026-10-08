using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace SnippyGrab.App.Services;

internal static class StartupLatencyChecks
{
    public static async Task Run(string destination)
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-startup-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            new SettingsService(Path.Combine(root, "settings.json")).Save(new Settings { FirstRunComplete = true, Animate = false, AutoCopy = false });
            using var controller = new AppController(true, root, diagnostic: true);
            using var process = Process.GetCurrentProcess();
            var processToControllerReadyMs = (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalMilliseconds;
            if (!double.IsFinite(processToControllerReadyMs) || processToControllerReadyMs < 0) throw new InvalidOperationException("Process startup clock is invalid.");
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 1200, 500));
                dc.DrawText(new FormattedText("Build failed\nError CS1002: semicolon expected", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), 32, Brushes.Black, 1), new Point(30, 70));
            }
            var image = new RenderTargetBitmap(1200, 500, 96, 96, PixelFormats.Pbgra32); image.Render(visual); image.Freeze();
            var png = ImageService.Png(image);
            var samples = new List<double>();
            for (var i = 0; i < 4; i++)
            {
                var watch = Stopwatch.StartNew();
                var text = await controller.OcrService.ReadAsync(png);
                if (!text.Contains("CS1002", StringComparison.Ordinal)) throw new InvalidOperationException("Startup OCR fixture did not recognize CS1002.");
                samples.Add(watch.Elapsed.TotalMilliseconds);
            }
            controller.Dispose(); controller.Dock.Close();
            AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(new { Result = "PASS", Build = BuildVersion.Display, ProcessToControllerReadyMs = processToControllerReadyMs, FirstOcrMs = samples[0], WarmOcr = TimingSummary.From(samples.Skip(1)), OcrSamplesMs = samples, OS = Environment.OSVersion.VersionString, ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), LogicalProcessors = Environment.ProcessorCount, Scope = "Fresh process/CLR to isolated controller ready (OS process timestamp), first and repeated native OCR with exact synthetic CS1002 recognition. File-system caches are uncontrolled. Excludes visible tray/global hotkey readiness, capture selection, OS clipboard and Snipping Tool comparison." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { Directory.Delete(root, true); }
    }
}
