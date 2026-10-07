using System.Diagnostics;
using System.Text.Json;

namespace SnippyGrab.App.Services;

internal static class PipelineLatencyChecks
{
    public static async Task Run(string destination)
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-latency-" + Guid.NewGuid().ToString("N"));
        var results = new List<object>();
        try
        {
            new SettingsService(Path.Combine(root, "settings.json")).Save(new Settings { FirstRunComplete = true, Animate = false });
            var startup = Stopwatch.StartNew();
            using var controller = new AppController(true, root, diagnostic: true);
            var startupMs = startup.Elapsed.TotalMilliseconds;
            controller.Clipboard = new ClipboardService(_ => { });
            foreach (var (width, height) in new[] { (1920, 1080), (3840, 2160), (7680, 4320) })
            {
                var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, new byte[checked(width * height * 4)], width * 4); image.Freeze();
                var samples = new List<double>();
                for (var i = 0; i < 10; i++)
                {
                    var watch = Stopwatch.StartNew(); var png = await Task.Run(() => ImageService.Png(image));
                    controller.Repository.Add(png, width, height); controller.Dock.Refresh();
                    if (!await controller.Clipboard.ImageAsync(image, true, png)) throw new InvalidOperationException("Synthetic clipboard sink failed.");
                    samples.Add(watch.Elapsed.TotalMilliseconds);
                }
                results.Add(new { Width = width, Height = height, FirstSampleMs = samples[0], Warm = TimingSummary.From(samples.Skip(1)), SamplesMs = samples });
            }
            controller.Dispose(); controller.Dock.Close();
            AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(new { Result = "PASS", ControllerStartupMs = startupMs, Measurements = results, OS = Environment.OSVersion.VersionString, ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), LogicalProcessors = Environment.ProcessorCount, Scope = "Synthetic encode/durable storage/dock rebuild/clipboard DataObject sink. Controller startup excludes CLR/process launch. No screen acquisition, selection wait, OS clipboard write or Snipping Tool comparison." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { Directory.Delete(root, true); }
    }
}
