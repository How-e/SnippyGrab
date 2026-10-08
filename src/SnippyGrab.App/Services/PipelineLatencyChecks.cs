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
                var drag = MeasureSingleImageDrag(controller.Repository, controller.Repository.Captures[0]);
                results.Add(new { Width = width, Height = height, FirstSampleMs = samples[0], Warm = TimingSummary.From(samples.Skip(1)), SamplesMs = samples, SingleImageDrag = drag });
            }
            controller.Dispose(); controller.Dock.Close();
            AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(new { Result = "PASS", ControllerStartupMs = startupMs, Measurements = results, OS = Environment.OSVersion.VersionString, ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), LogicalProcessors = Environment.ProcessorCount, Scope = "Synthetic encode/durable storage/dock rebuild/clipboard DataObject sink. Controller startup excludes CLR/process launch. No screen acquisition, selection wait, OS clipboard write or Snipping Tool comparison." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { Directory.Delete(root, true); }
    }
    private static object MeasureSingleImageDrag(CaptureRepository repository, CaptureRecord record)
    {
        using var lease = repository.Lease([record]);
        var path = repository.PathFor(record); var expected = File.ReadAllBytes(path);
        var before = new List<double>(); var after = new List<double>();
        var beforeBytes = new List<long>(); var afterBytes = new List<long>();
        var service = new DragDropService(repository);
        DataObject LegacyData()
        {
            // Retain the previous production path solely as a within-process comparator.
            var files = TransferPayload.Files(repository, [record]);
            var data = new DataObject(); data.SetData(DataFormats.FileDrop, files);
            var image = ImageService.Load(files[0]); data.SetImage(image);
            data.SetData("PNG", new MemoryStream(ImageService.Png(image))); return data;
        }
        void Measure(bool legacy)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
            var data = legacy ? LegacyData() : service.BuildData([record]);
            watch.Stop(); var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            (legacy ? before : after).Add(watch.Elapsed.TotalMilliseconds);
            (legacy ? beforeBytes : afterBytes).Add(bytes);
            if (!legacy && (data.GetData("PNG") is not MemoryStream png || !expected.AsSpan().SequenceEqual(png.ToArray()))) throw new InvalidOperationException("Drag must retain the stored PNG bytes.");
        }
        for (var i = 0; i < 6; i++)
        {
            // Alternate order to reduce a consistent warm-cache/order advantage.
            Measure(i % 2 == 0); Measure(i % 2 != 0);
        }
        return new
        {
            ReencodedPng = TimingSummary.From(before.Skip(1)),
            StoredPng = TimingSummary.From(after.Skip(1)),
            ReencodedMedianAllocatedBytes = beforeBytes.Skip(1).Order().ElementAt(2),
            StoredMedianAllocatedBytes = afterBytes.Skip(1).Order().ElementAt(2),
            Scope = "Six alternating synthetic single-image DataObject builds per path, first sample excluded. Previous re-encoding path compared with stored bytes; no OLE receiver or OS clipboard. Allocations count managed bytes on this thread, excluding native WIC allocations."
        };
    }
}
