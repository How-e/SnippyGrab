namespace SnippyGrab.Core;

public sealed record CaptureExportResult(string Path, bool MetadataSaved);

public static class CaptureExport
{
    public static CaptureExportResult Write(CaptureRepository repository, CaptureRecord capture, string destination)
    {
        if (!Path.IsPathFullyQualified(destination)) throw new InvalidDataException("Choose an absolute export path.");
        var path = Path.GetFullPath(destination);
        if (!Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Choose a filename ending in .png.");
        var root = Path.TrimEndingDirectorySeparator(repository.Root);
        if (path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a location outside the managed cache.");
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(path)!); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Export cannot use redirected directories.");
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Export cannot replace a redirected file.");
        if (!repository.Captures.Contains(capture)) throw new InvalidOperationException("Capture is no longer current. Select it again before exporting.");
        using var lease = repository.Lease([capture]);
        AtomicFile.Write(path, File.ReadAllBytes(repository.PathFor(capture)));
        capture.Saved = true; capture.ExportPath = path;
        try { repository.Persist(); return new(path, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { return new(path, false); } // The PNG exists; do not misreport a nonessential metadata failure as export loss.
    }
}
