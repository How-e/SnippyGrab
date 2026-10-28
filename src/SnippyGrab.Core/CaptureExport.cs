namespace SnippyGrab.Core;

public sealed record CaptureExportResult(string Path, bool MetadataSaved);
public enum ExportFormat { Png, Jpeg, WebpLossless, WebpLossy }

public static class CaptureExport
{
    public static string Extension(ExportFormat format) => format switch { ExportFormat.Png => ".png", ExportFormat.Jpeg => ".jpg", ExportFormat.WebpLossless or ExportFormat.WebpLossy => ".webp", _ => throw new InvalidDataException("Unsupported export format.") };
    public static CaptureExportResult Write(CaptureRepository repository, CaptureRecord capture, string destination)
    {
        var path = ValidateDestination(repository.Root, destination, ExportFormat.Png);
        var revision = capture.FileName; EnsureCurrent(repository, capture, revision);
        using var lease = repository.Lease([capture]);
        AtomicFile.WriteChecked(path, output =>
        {
            var source = repository.PathFor(capture); ManagedPath.RejectRedirects(source);
            using var input = File.OpenRead(source); input.CopyTo(output);
            EnsureCurrent(repository, capture, revision);
        }, true, () => CheckDestination(repository.Root, path, ExportFormat.Png));
        return Remember(repository, capture, path);
    }
    public static async Task<CaptureExportResult> WriteAsync(CaptureRepository repository, CaptureRecord capture, string destination,
        ExportFormat format, int quality, Func<string, int, byte[]> encodeJpeg, CancellationToken cancellation = default, bool overwrite = false)
    {
        if (quality is < 1 or > 100) throw new InvalidDataException("Export quality must be between 1 and 100.");
        var path = ValidateDestination(repository.Root, destination, format);
        var revision = capture.FileName; EnsureCurrent(repository, capture, revision);
        using var lease = repository.Lease([capture]);
        var source = repository.PathFor(capture); var cache = repository.Root;
        if (format == ExportFormat.Png)
        {
            await AtomicFile.WriteCheckedAsync(path, async (output, token) =>
            {
                ManagedPath.RejectRedirects(source);
                await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read,
                    81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                await input.CopyToAsync(output, token).ConfigureAwait(false);
            }, overwrite, () => CheckDestination(cache, path, format),
                () => EnsureCurrent(repository, capture, revision), cancellation);
        }
        else
        {
            var bytes = await Task.Run(() => { ManagedPath.RejectRedirects(source); return encodeJpeg(source, quality); }, cancellation);
            cancellation.ThrowIfCancellationRequested(); EnsureCurrent(repository, capture, revision);
            await Task.Run(() =>
            {
                cancellation.ThrowIfCancellationRequested();
                WriteDestination(cache, path, format, bytes, overwrite);
            }, cancellation);
        }
        // A write is durable even when metadata is stale or persistence fails.
        if (!repository.Captures.Contains(capture) || capture.FileName != revision) return new(path, false);
        return await RememberAsync(repository, capture, path, revision);
    }
    internal static async Task<CaptureExportResult> RememberAsync(CaptureRepository repository, CaptureRecord capture, string path, string? revision = null)
    {
        try { await repository.RememberExportAsync(capture, path, revision); return new(path, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException) { return new(path, false); }
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
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Export cannot replace a redirected file.");
        var resolved = Path.Combine(ManagedPath.ResolveDirectory(Path.GetDirectoryName(path)!), Path.GetFileName(path));
        root = ManagedPath.ResolveDirectory(root);
        if (resolved.Equals(root, StringComparison.OrdinalIgnoreCase) || resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a location outside the managed cache.");
        return resolved;
    }
    internal static void WriteDestination(string cache, string path, ExportFormat format, byte[] bytes, bool overwrite)
        => AtomicFile.WriteChecked(path, bytes, overwrite, () => CheckDestination(cache, path, format));
    private static void CheckDestination(string cache, string path, ExportFormat format)
    {
        if (!ValidateDestination(cache, path, format).Equals(path, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Export directory changed. Choose the destination again.");
    }
}
