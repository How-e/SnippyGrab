using System.Text.Json;
using System.Text.RegularExpressions;

namespace SnippyGrab.Core;

public sealed class CaptureRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public int Width { get; set; }
    public int Height { get; set; }
    public string Monitor { get; set; } = "";
    public bool Pinned { get; set; }
    public bool Edited { get; set; }
    public bool Saved { get; set; }
    public bool Dismissed { get; set; }
}
public sealed class RepositoryState
{
    public int SchemaVersion { get; set; } = 1;
    public List<CaptureRecord> Captures { get; set; } = [];
    public Dictionary<string, DateTimeOffset> ProtectedUntil { get; set; } = [];
}

public sealed partial class CaptureRepository
{
    private readonly string root;
    private readonly Dictionary<string, int> leases = new(StringComparer.OrdinalIgnoreCase);
    private RepositoryState state = new();
    public IReadOnlyList<CaptureRecord> Captures => state.Captures;
    public string Root => root;
    public bool HistoryEnabled { get; set; } = true;
    public bool Recovered { get; private set; }
    public bool CleanupBlocked { get; private set; }
    public CaptureRepository(string directory)
    {
        root = Path.GetFullPath(directory);
        if (root.StartsWith(@"\\", StringComparison.Ordinal) || root == Path.GetPathRoot(root))
            throw new InvalidDataException("Cache must be a dedicated local directory.");
        Directory.CreateDirectory(root);
        // Reject symlinks/junctions in every parent, preventing cleanup from crossing a redirected root.
        for (var current = new DirectoryInfo(root); current is not null; current = current.Parent)
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Cache cannot use redirected directories.");
    }
    [GeneratedRegex(@"^capture-[a-f0-9]{32}\.png\z", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
    [GeneratedRegex(@"^(capture-[a-f0-9]{32}\.png|history\.json)\.[a-f0-9]{32}\.tmp\z", RegexOptions.CultureInvariant)]
    private static partial Regex TemporaryPattern();
    public static bool IsSafeName(string? name) => name is not null && NamePattern().IsMatch(name);
    public string PathFor(CaptureRecord capture) => PathForName(capture.FileName);
    public string PathForName(string name)
    {
        if (!IsSafeName(name)) throw new InvalidDataException("Invalid capture filename.");
        return Path.Combine(root, name);
    }
    private bool SafeFile(string path) => File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
    public void Load()
    {
        var metadata = Path.Combine(root, "history.json");
        try
        {
            if (File.Exists(metadata))
            {
                if (!SafeFile(metadata)) throw new InvalidDataException("Invalid metadata location.");
                if (new FileInfo(metadata).Length > 4 * 1024 * 1024) throw new InvalidDataException("History too large.");
                state = JsonSerializer.Deserialize<RepositoryState>(File.ReadAllText(metadata)) ?? new();
                if (state.SchemaVersion != 1) throw new InvalidDataException("Unsupported history version.");
                if (state.Captures is null || state.ProtectedUntil is null) throw new InvalidDataException("Invalid history state.");
                state.Captures = state.Captures.Where(c => IsSafeName(c.FileName) && c.Width > 0 && c.Height > 0 && SafeFile(PathFor(c)))
                    .DistinctBy(c => c.Id).ToList();
                state.ProtectedUntil = state.ProtectedUntil.Where(p => IsSafeName(p.Key) && p.Value > DateTimeOffset.UtcNow).ToDictionary();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or ArgumentException)
        { state = new(); Recovered = true; CleanupBlocked = true; }
        var known = state.Captures.Select(c => c.FileName).ToHashSet();
        foreach (var path in Directory.EnumerateFiles(root, "capture-*.png").Take(2000))
        {
            var name = Path.GetFileName(path);
            if (!IsSafeName(name) || !SafeFile(path) || known.Contains(name)) continue;
            // Recovery is metadata-only; the UI validates dimensions when decoding, with a pixel limit.
            if (HistoryEnabled) state.Captures.Add(new() { FileName = name, CreatedUtc = File.GetLastWriteTimeUtc(path), Width = 1, Height = 1, Dismissed = true });
        }
    }
    public CaptureRecord Add(byte[] png, int width, int height, string monitor = "")
    {
        var record = new CaptureRecord { FileName = "capture-" + Guid.NewGuid().ToString("N") + ".png", Width = width, Height = height, Monitor = monitor };
        AtomicFile.Write(PathFor(record), png);
        state.Captures.Insert(0, record);
        Persist();
        return record;
    }
    public void Replace(CaptureRecord record, byte[] png, int width, int height)
    {
        // Immutable file identity keeps existing receiver/clipboard payloads intact after editing.
        var name = "capture-" + Guid.NewGuid().ToString("N") + ".png";
        AtomicFile.Write(PathForName(name), png);
        record.FileName = name; record.Width = width; record.Height = height; record.Edited = true;
        Persist();
    }
    public void Persist()
    {
        var saved = new RepositoryState
        {
            Captures = state.Captures.Where(c => HistoryEnabled || c.Pinned).ToList(),
            ProtectedUntil = state.ProtectedUntil
        };
        AtomicFile.Write(Path.Combine(root, CleanupBlocked ? "history-recovered.json" : "history.json"), JsonSerializer.SerializeToUtf8Bytes(saved));
    }
    public IDisposable Lease(IEnumerable<CaptureRecord> records, bool transfer = false)
    {
        var names = records.Select(c => c.FileName).Distinct().ToArray();
        foreach (var name in names)
        {
            PathForName(name);
            leases[name] = leases.GetValueOrDefault(name) + 1;
            if (transfer) state.ProtectedUntil[name] = DateTimeOffset.UtcNow.AddHours(24);
        }
        if (transfer) Persist();
        return new ReleaseLease(() =>
        {
            foreach (var name in names)
            {
                if (leases.TryGetValue(name, out var count)) { if (count <= 1) leases.Remove(name); else leases[name] = count - 1; }
                if (transfer) state.ProtectedUntil[name] = DateTimeOffset.UtcNow.AddHours(24);
            }
            if (transfer) Persist();
        });
    }
    public int Cleanup(DateTimeOffset now, int retentionHours, bool clear = false)
    {
        if (CleanupBlocked) return 0; // Corrupt pin metadata must never turn into permission to delete images.
        var pinned = state.Captures.Where(c => c.Pinned).Select(c => c.FileName).ToHashSet();
        var removed = 0;
        foreach (var path in Directory.EnumerateFiles(root, "capture-*.png"))
        {
            var name = Path.GetFileName(path);
            if (!IsSafeName(name) || !SafeFile(path) || pinned.Contains(name) || leases.ContainsKey(name) || state.ProtectedUntil.GetValueOrDefault(name) > now) continue;
            var record = state.Captures.FirstOrDefault(c => c.FileName == name);
            var created = record?.CreatedUtc ?? new DateTimeOffset(File.GetLastWriteTimeUtc(path));
            if (!clear && (retentionHours < 0 || created.AddHours(retentionHours) > now)) continue;
            try { File.Delete(path); state.Captures.RemoveAll(c => c.FileName == name); removed++; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        foreach (var key in state.ProtectedUntil.Where(p => p.Value <= now).Select(p => p.Key).ToArray()) state.ProtectedUntil.Remove(key);
        // Remove only old, owned incomplete atomic writes, never unknown files.
        foreach (var path in Directory.EnumerateFiles(root, "*.tmp"))
            if (SafeFile(path) && File.GetLastWriteTimeUtc(path) < now.UtcDateTime.AddDays(-1) &&
                TemporaryPattern().IsMatch(Path.GetFileName(path)))
                try { File.Delete(path); } catch (IOException) { }
        Persist();
        return removed;
    }
    private sealed class ReleaseLease(Action release) : IDisposable
    {
        private Action? action = release;
        public void Dispose() => Interlocked.Exchange(ref action, null)?.Invoke();
    }
}

public static class TransferPayload
{
    public static string[] Files(CaptureRepository repository, IEnumerable<CaptureRecord> records)
    {
        var paths = records.Select(repository.PathFor).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length == 0 || paths.Any(p => !File.Exists(p))) throw new IOException("Capture file is no longer available.");
        return paths;
    }
}
public static class StartupCommand
{
    public static string Build(string executable)
    {
        if (!Path.IsPathFullyQualified(executable) || executable.Contains('"') || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Startup executable must be an absolute executable path.");
        return $"\"{executable}\" --background";
    }
}
