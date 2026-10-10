namespace SnippyGrab.Core;

public enum ExportCollision { Unique, Skip, Replace }
public sealed record BatchExportItem(CaptureRecord Capture, string Revision, string Source, string Destination, bool Skip);
public sealed record BatchExportResult(int Successful, int Skipped, int Failed, int Unprocessed, int MetadataWarnings)
{
    public IReadOnlyList<string> Errors { get; init; } = [];
    public override string ToString() => $"Successful: {Successful}; skipped: {Skipped}; failed: {Failed}; unprocessed: {Unprocessed}; history warnings: {MetadataWarnings}. Completed files are retained." + (Errors.Count == 0 ? "" : "\n\n" + string.Join("\n", Errors));
}
public static class BatchExport
{
    public static IReadOnlyList<BatchExportItem> Plan(CaptureRepository repository, IEnumerable<CaptureRecord> selected, string folder, ExportFormat format, ExportCollision collision)
    {
        if (!Enum.IsDefined(collision)) throw new InvalidDataException("Choose a collision policy.");
        var ordered = TransferPayload.Ordered(repository, selected.ToArray());
        if (ordered.Count is < 1 or > 200) throw new InvalidDataException("Select between 1 and 200 captures.");
        var result = new List<BatchExportItem>(); var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var extension = CaptureExport.Extension(format);
        foreach (var capture in ordered)
        {
            var name = $"{result.Count + 1:D3}-SnippyGrab-{capture.CreatedUtc.UtcDateTime:yyyyMMdd-HHmmss}";
            var destination = Path.Combine(folder, name + extension); var suffix = 1;
            if (collision == ExportCollision.Unique)
                while (File.Exists(destination) || Directory.Exists(destination) || reserved.Contains(destination)) destination = Path.Combine(folder, name + $"-{suffix++}" + extension);
            destination = CaptureExport.ValidateDestination(repository.Root, destination, format); reserved.Add(destination);
            result.Add(new(capture, capture.FileName, repository.PathFor(capture), destination, collision == ExportCollision.Skip && (File.Exists(destination) || Directory.Exists(destination))));
        }
        return result;
    }
    public static async Task<BatchExportResult> RunAsync(CaptureRepository repository, IReadOnlyList<BatchExportItem> plan, ExportFormat format, int quality,
        ExportCollision collision, Func<string, int, byte[]> encode, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        if (quality is < 1 or > 100 || plan.Count is < 1 or > 200) throw new InvalidDataException("Invalid batch export settings.");
        using var leases = repository.Lease(plan.Select(p => p.Capture));
        // Caller creates the plan and starts this operation without yielding; all revisions are now leased.
        if (plan.Any(p => p.Capture.FileName != p.Revision)) throw new InvalidOperationException("Captures changed before batch export started. Select them again.");
        int success = 0, skipped = 0, failed = 0, warnings = 0, processed = 0;
        var errors = new List<string>();
        foreach (var item in plan)
        {
            if (cancellation.IsCancellationRequested) break;
            if (item.Skip) { skipped++; processed++; continue; }
            progress?.Report($"Exporting {processed + 1} of {plan.Count}…");
            try
            {
                var bytes = await Task.Run(() => { ManagedPath.RejectRedirects(item.Source); return format == ExportFormat.Png ? File.ReadAllBytes(item.Source) : encode(item.Source, quality); }, cancellation);
                var written = await Task.Run(() =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    CaptureExport.ValidateDestination(repository.Root, item.Destination, format);
                    if (collision == ExportCollision.Skip && (File.Exists(item.Destination) || Directory.Exists(item.Destination))) return false;
                    try { AtomicFile.Write(item.Destination, bytes, collision == ExportCollision.Replace); }
                    catch (IOException) when (collision == ExportCollision.Skip && (File.Exists(item.Destination) || Directory.Exists(item.Destination))) { return false; }
                    return true;
                }, cancellation);
                if (!written) skipped++;
                else
                {
                    success++;
                    if (item.Capture.FileName != item.Revision || !repository.Captures.Contains(item.Capture) || !CaptureExport.Remember(repository, item.Capture, item.Destination).MetadataSaved) warnings++;
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
            { failed++; var error = $"Item {processed + 1} ({Path.GetFileName(item.Destination)}): {OperationFailure.From(ex).Message}"; errors.Add(error); progress?.Report(error); }
            processed++;
        }
        return new(success, skipped, failed, plan.Count - processed, warnings) { Errors = errors.ToArray() };
    }
}
