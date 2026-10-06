using System.Diagnostics;
using System.Text.Json;
using SnippyGrab.App.Views;

namespace SnippyGrab.App.Services;

internal static class EditorPerformanceChecks
{
    public static async Task Run(string destination)
    {
        var results = new List<object>();
        foreach (var (width, height) in new[] { (1920, 1080), (3840, 2160), (7680, 4320) })
        {
            results.Add(await Measure(width, height));
            GC.Collect(); GC.WaitForPendingFinalizers();
        }
        AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(new { Result = "PASS", Measurements = results, Scope = "Synthetic editor render/crop/undo retained-memory budget; effects materialize on workers; excludes hardware capture, clipboard, prolonged stress and a strict UI latency target." }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static async Task<object> Measure(int width, int height)
    {
        var baseImage = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, new byte[checked(width * height * 4)], width * 4); baseImage.Freeze();
        var initial = new EditorState(baseImage, []);
        var points = Enumerable.Range(0, 20000).Select(i => new Point(i % width, 100 + i % 80)).ToArray();
        var mark = new Annotation(EditTool.Pen, points[0], points[^1], Colors.Red, 3, 24, "", points);
        var watch = Stopwatch.StartNew(); var allocations = GC.GetTotalAllocatedBytes();
        var state = await EditorWindow.BuildStateAsync(initial, mark);
        var image = await EditorWindow.RenderAsync(state);
        var renderMs = watch.ElapsedMilliseconds; var renderAllocations = GC.GetTotalAllocatedBytes() - allocations;
        var journal = new UndoJournal<EditorState>(state, 20, EditorWindow.RetainedBytes, 256L * 1024 * 1024);
        var crop = new Annotation(EditTool.Crop, new Point(0, 0), new Point(width - 10, height - 10), Colors.Red, 3, 24, "", []);
        watch.Restart();
        for (var i = 0; i < 3; i++) journal.Push(await EditorWindow.BuildStateAsync(journal.Current, crop with { End = new Point(journal.Current.Base.PixelWidth - 10, journal.Current.Base.PixelHeight - 10) }));
        var cropMs = watch.ElapsedMilliseconds;
        if (journal.Current.Base.PixelWidth != width - 30) throw new InvalidOperationException("Large image crop coordinates failed.");
        var undoSteps = 0; while (journal.CanUndo) { journal.Undo(); undoSteps++; }
        while (journal.CanRedo) journal.Redo();
        var effect = mark with { Tool = EditTool.Blur, Start = new Point(20, 20), End = new Point(220, 180), Points = [] };
        watch.Restart(); var effected = await EditorWindow.BuildStateAsync(journal.Current, effect);
        if (effected.Marks[^1].Patch is not { IsFrozen: true }) throw new InvalidOperationException("Worker effect must be frozen.");
        return new { Width = width, Height = height, StrokePoints = points.Length, RenderMs = renderMs, RenderAllocatedBytes = renderAllocations, ThreeCropMs = cropMs, Blur200x160Ms = watch.ElapsedMilliseconds, UndoStepsRetained = undoSteps, UndoBudgetBytes = 256L * 1024 * 1024, OutputFrozen = image.IsFrozen };
    }
}
