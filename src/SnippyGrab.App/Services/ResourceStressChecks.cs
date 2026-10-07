using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SnippyGrab.App.Views;

namespace SnippyGrab.App.Services;

internal static class ResourceStressChecks
{
    [DllImport("user32.dll")] private static extern uint GetGuiResources(nint process, uint flags);
    public static async Task Run(string destination, int seconds)
    {
        if (seconds is < 1 or > 86400) throw new ArgumentOutOfRangeException(nameof(seconds));
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-stress-" + Guid.NewGuid().ToString("N"));
        var samples = new List<object>(); var cycles = 0; var ocrRuns = 0; var clock = Stopwatch.StartNew();
        try
        {
            new SettingsService(Path.Combine(root, "settings.json")).Save(new Settings { FirstRunComplete = true, Animate = false, RetentionHours = -1, DockLifetimeMinutes = 0 });
            using var controller = new AppController(true, root, diagnostic: true);
            var image = BitmapSource.Create(640, 360, 96, 96, PixelFormats.Bgra32, null, new byte[640 * 360 * 4], 640 * 4); image.Freeze();
            var png = ImageService.Png(image); var process = Process.GetCurrentProcess();
            object Sample()
            {
                process.Refresh(); return new { Seconds = clock.Elapsed.TotalSeconds, Captures = controller.Repository.Captures.Count, WorkingBytes = process.WorkingSet64, PrivateBytes = process.PrivateMemorySize64, CpuMs = process.TotalProcessorTime.TotalMilliseconds, Handles = process.HandleCount, GdiHandles = GetGuiResources(process.Handle, 0), UserHandles = GetGuiResources(process.Handle, 1), ManagedBytes = GC.GetTotalMemory(false) };
            }
            void Report(string result) => AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(new { Result = result, RequestedSeconds = seconds, ElapsedSeconds = clock.Elapsed.TotalSeconds, Cycles = cycles, OcrRuns = ocrRuns, Samples = samples, Scope = "Synthetic 640x360 storage/history=never, hidden dock rebuild, worker effects, three offscreen pin windows per 25 cycles and real local OCR. No input, desktop capture or OS clipboard writes. A short run is not hours-long acceptance." }, new JsonSerializerOptions { WriteIndented = true }));
            samples.Add(Sample()); Report("RUNNING");
            do
            {
                var record = controller.Repository.Add(png, 640, 360); controller.Dock.Refresh(); cycles++;
                if (cycles % 25 == 0)
                {
                    var pins = controller.Repository.Captures.Take(3).Select(c => new PinWindow(controller, c) { Left = -30000, Top = -30000, Topmost = false }).ToArray();
                    try
                    {
                        foreach (var pin in pins) pin.Show();
                        var state = new EditorState(image, []);
                        var effect = new Annotation(EditTool.Blur, new Point(10, 10), new Point(110, 90), Colors.Black, 3, 24, "", []);
                        var edited = await EditorWindow.BuildStateAsync(state, effect);
                        var rendered = await EditorWindow.RenderAsync(edited);
                        controller.Repository.Replace(record, await Task.Run(() => ImageService.Png(rendered)), 640, 360, record.FileName);
                    }
                    finally { foreach (var pin in pins) pin.Close(); }
                    if (cycles % 100 == 0) { await controller.OcrService.ReadAsync(png); ocrRuns++; }
                    samples.Add(Sample()); Report("RUNNING");
                }
                await Task.Delay(cycles < 300 ? 1 : 1000);
            } while (cycles < 300 || clock.Elapsed.TotalSeconds < seconds);
            GC.Collect(); GC.WaitForPendingFinalizers(); await Task.Delay(100); samples.Add(Sample());
            controller.Dispose(); controller.Dock.Close(); samples.Add(Sample()); Report("PASS");
        }
        finally { Directory.Delete(root, true); }
    }
}
