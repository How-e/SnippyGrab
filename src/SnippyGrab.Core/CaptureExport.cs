namespace SnippyGrab.Core;

public sealed record CaptureExportResult(string Path, bool MetadataSaved);
public enum ExportFormat { Png, Jpeg, WebpLossless, WebpLossy }

public static class CaptureExport
{
    public static string Extension(ExportFormat format) => format switch { ExportFormat.Png => ".png", ExportFormat.Jpeg => ".jpg", ExportFormat.WebpLossless or ExportFormat.WebpLossy => ".webp", _ => throw new InvalidDataException("Unsupported export format.") };
    public static CaptureExportResult Write(CaptureRepository repository, CaptureRecord capture, string destination)
    {
        var path = ValidateDestination(repository.Root, destination, ExportFormat.Png);
        EnsureCurrent(repository, capture, capture.FileName);
        using var lease = repository.Lease([capture]);
        AtomicFile.Write(path, File.ReadAllBytes(repository.PathFor(capture)));
        return Remember(repository, capture, path);
    }
    public static async Task<CaptureExportResult> WriteAsync(CaptureRepository repository, CaptureRecord capture, string destination,
        ExportFormat format, int quality, Func<string, int, byte[]> encodeJpeg, CancellationToken cancellation = default)
    {
        if (quality is < 1 or > 100) throw new InvalidDataException("Export quality must be between 1 and 100.");
        var path = ValidateDestination(repository.Root, destination, format);
        var revision = capture.FileName; EnsureCurrent(repository, capture, revision);
        using var lease = repository.Lease([capture]);
        var source = repository.PathFor(capture); var cache = repository.Root;
        var bytes = await Task.Run(() =>
        {
            ManagedPath.RejectRedirects(source);
            return format == ExportFormat.Png ? File.ReadAllBytes(source) : encodeJpeg(source, quality);
        }, cancellation);
        cancellation.ThrowIfCancellationRequested(); EnsureCurrent(repository, capture, revision);
        await Task.Run(() =>
        {
            cancellation.ThrowIfCancellationRequested();
            ValidateDestination(cache, path, format); // Recheck policy immediately before the atomic write.
            AtomicFile.Write(path, bytes);
        }, cancellation);
        // A write is durable even when metadata is stale or persistence fails.
        if (!repository.Captures.Contains(capture) || capture.FileName != revision) return new(path, false);
        return Remember(repository, capture, path);
    }
    private static void EnsureCurrent(CaptureRepository repository, CaptureRecord capture, string revision)
    {
        if (!repository.Captures.Contains(capture) || capture.FileName != revision) throw new InvalidOperationException("Capture changed. Select it again before exporting.");
    }
    internal static CaptureExportResult Remember(CaptureRepository repository, CaptureRecord capture, string path)
    {
        capture.Saved = true; capture.ExportPath = path;
        try { repository.Persist(); return new(path, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { return new(path, false); }
    }
    public static string ValidateDestination(string cache, string destination, ExportFormat format)
    {
        if (!Enum.IsDefined(format)) throw new InvalidDataException("Unsupported export format.");
        if (!Path.IsPathFullyQualified(destination)) throw new InvalidDataException("Choose an absolute export path.");
        var path = Path.GetFullPath(destination);
        var extension = Path.GetExtension(path);
        if (!extension.Equals(Extension(format), StringComparison.OrdinalIgnoreCase) && !(format == ExportFormat.Jpeg && extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("The filename extension must match the selected export format.");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(cache));
        if (path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a location outside the managed cache.");
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(path)!); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Export cannot use redirected directories.");
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Export cannot replace a redirected file.");
        return path;
    }
}
