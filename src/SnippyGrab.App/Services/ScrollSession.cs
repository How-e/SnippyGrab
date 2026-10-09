using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SnippyGrab.App.Services;

internal sealed class ScrollSession : IDisposable
{
    internal const long DiskBudget = 256L * 1024 * 1024;
    private readonly string root;
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly List<string> frames = [];
    private readonly List<int> overlaps = [];
    internal IReadOnlyList<string> Frames => frames;
    internal string? Pending { get; private set; }
    private long staged;
    internal ScrollSession(string parent)
    {
        ManagedPath.RejectRedirects(parent); Directory.CreateDirectory(parent); Sweep(parent);
        root = Path.Combine(parent, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "owner.txt"), "SnippyGrab-scroll-v1");
    }
    internal static void Sweep(string parent)
    {
        foreach (var folder in Directory.EnumerateDirectories(parent))
        {
            if (!Regex.IsMatch(Path.GetFileName(folder), "^[a-f0-9]{32}$") || Directory.GetCreationTimeUtc(folder) > DateTime.UtcNow.AddHours(-24)) continue;
            try
            {
                ManagedPath.RejectRedirects(folder); if (Directory.EnumerateDirectories(folder).Any()) continue;
                var files = Directory.GetFiles(folder); if (files.Length > 100 || files.Any(f => Path.GetFileName(f) != "owner.txt" && !Regex.IsMatch(Path.GetFileName(f), "^(frame|part)-[a-f0-9]{32}\\.png$"))) continue;
                foreach (var file in files) ManagedPath.RejectRedirects(file);
                var marker = Path.Combine(folder, "owner.txt"); if (new FileInfo(marker).Length > 64 || File.ReadAllText(marker) != "SnippyGrab-scroll-v1") continue;
                foreach (var file in files) File.Delete(file); Directory.Delete(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
        }
    }
    internal void CheckBudget()
    {
        if (elapsed.Elapsed > TimeSpan.FromMinutes(10)) throw new InvalidOperationException("Scrolling session expired after 10 minutes. Start a new session.");
        if (frames.Count >= 30) throw new InvalidOperationException("Scrolling session reached 30 frames. Finish or start a new session.");
    }
    internal void Stage(BitmapSource image)
    {
        CheckBudget(); if (Pending is not null) throw new InvalidOperationException("Accept or reject the pending frame first.");
        if (image.PixelWidth > 4096 || image.PixelHeight > 4096 || (long)image.PixelWidth * image.PixelHeight > 8_000_000) throw new InvalidDataException("Scrolling viewport exceeds 4096 pixels per axis or 8 MP.");
        var bytes = ImageService.Png(image); if (staged + bytes.Length > DiskBudget) throw new InvalidDataException("Scrolling temporary storage exceeds 256 MiB.");
        var path = Path.Combine(root, "frame-" + Guid.NewGuid().ToString("N") + ".png"); AtomicFile.Write(path, bytes); staged += bytes.Length;
        if (frames.Count == 0) { frames.Add(path); overlaps.Add(0); } else Pending = path;
    }
    internal void Accept(int overlap, int frameHeight)
    {
        if (Pending is null || overlap < 0 || overlap >= frameHeight) throw new InvalidDataException("Choose a valid overlap smaller than the frame.");
        frames.Add(Pending); overlaps.Add(overlap); Pending = null;
    }
    internal void Reject() { if (Pending is not null) { Remove(Pending); Pending = null; } }
    internal void Undo() { Reject(); if (frames.Count <= 1) return; var last = frames[^1]; frames.RemoveAt(frames.Count - 1); overlaps.RemoveAt(overlaps.Count - 1); Remove(last); }
    private void Remove(string path) { staged -= new FileInfo(path).Length; File.Delete(path); }
    internal BitmapSource Build(int top, int bottom, CancellationToken cancellation)
    {
        if (Pending is not null) throw new InvalidOperationException("Accept or reject the pending seam before finishing.");
        if (elapsed.Elapsed > TimeSpan.FromMinutes(10)) throw new InvalidOperationException("Scrolling session expired.");
        var parts = new List<string>(); var dimensions = new List<(int, int)>();
        try
        {
            for (var i = 0; i < frames.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested(); var image = ImageService.Load(frames[i]);
                var trim = top + overlaps[i]; var height = image.PixelHeight - trim - bottom;
                if (height <= 0) throw new InvalidDataException("Header/footer and overlap remove the entire frame.");
                var cropped = ImageService.Crop(image, new(0, trim, image.PixelWidth, height));
                dimensions.Add((cropped.PixelWidth, cropped.PixelHeight));
                Composition.Plan(dimensions, new()); // Reject oversize before creating more intermediates.
                var bytes = ImageService.Png(cropped);
                if (staged + bytes.Length > DiskBudget) throw new InvalidDataException("Scrolling staging quota exceeded.");
                var path = Path.Combine(root, "part-" + Guid.NewGuid().ToString("N") + ".png"); AtomicFile.Write(path, bytes); staged += bytes.Length; parts.Add(path);
            }
            return CompositionService.Render(parts, Composition.Plan(dimensions, new()), Colors.Transparent, cancellation);
        }
        finally { foreach (var part in parts) Remove(part); }
    }
    public void Dispose()
    {
        if (!Directory.Exists(root)) return;
        ManagedPath.RejectRedirects(root);
        foreach (var file in Directory.GetFiles(root)) { ManagedPath.RejectRedirects(file); File.Delete(file); }
        Directory.Delete(root);
    }
}
