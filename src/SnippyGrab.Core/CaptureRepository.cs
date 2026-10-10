using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SnippyGrab.Core;

public sealed class CaptureRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RestoredUtc { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool DimensionsPending { get; set; }
    public string Monitor { get; set; } = "";
    public bool Pinned { get; set; }
    public bool Edited { get; set; }
    public bool Saved { get; set; }
    public string ExportPath { get; set; } = "";
    public bool Dismissed { get; set; }
    public PinLayout? PinLayout { get; set; }
}
public sealed class RepositoryState
{
    public int SchemaVersion { get; set; } = 1;
    public List<string> Pages { get; set; } = [];
    public List<CaptureRecord> Captures { get; set; } = [];
    public Dictionary<string, DateTimeOffset> Superseded { get; set; } = [];
    public Dictionary<string, DateTimeOffset> ProtectedUntil { get; set; } = [];
}

public sealed partial class CaptureRepository
{
    private readonly string root;
    private readonly Action<string, byte[]> write;
    private readonly Func<DateTimeOffset> clock;
    private readonly HashSet<string> sessionFiles = new(StringComparer.OrdinalIgnoreCase);
    public bool PersistencePending { get; private set; }
    public event Action? PersistenceFailed;
    public event Action? Changed;
    public event Action<CaptureRecord>? RevisionChanged;
    private readonly Dictionary<string, int> leases = new(StringComparer.OrdinalIgnoreCase);
    private RepositoryState state = new();
    private bool recoveryNeedsBackup;
    private HashSet<string> committedPages = [];
    private readonly Dictionary<Guid, CachedPage> pageCache = [];
    private readonly SemaphoreSlim cleanupSerial = new(1);
    private readonly record struct CaptureStamp(Guid Id, string File, DateTimeOffset Created, DateTimeOffset? Restored, int Width, int Height,
        bool DimensionsPending, string Monitor, bool Pinned, bool Edited, bool Saved, string ExportPath, bool Dismissed, PinLayout? PinLayout)
    {
        internal static CaptureStamp From(CaptureRecord c) => new(c.Id, c.FileName, c.CreatedUtc, c.RestoredUtc, c.Width, c.Height,
            c.DimensionsPending, c.Monitor, c.Pinned, c.Edited, c.Saved, c.ExportPath, c.Dismissed, c.PinLayout);
    }
    private sealed record CachedPage(string Name, CaptureStamp[] Stamps);
    public IReadOnlyList<CaptureRecord> Captures => state.Captures;
    public string Root => root;
    public bool HistoryEnabled { get; set; } = true;
    public bool Recovered { get; private set; }
    public bool CleanupBlocked { get; private set; }
    public CaptureRepository(string directory, Action<string, byte[]>? writer = null, Func<DateTimeOffset>? utcNow = null)
    {
        write = writer ?? AtomicFile.Write; clock = utcNow ?? (() => DateTimeOffset.UtcNow);
        root = Path.GetFullPath(directory);
        if (root.StartsWith(@"\\", StringComparison.Ordinal) || root == Path.GetPathRoot(root))
            throw new InvalidDataException("Cache must be a dedicated local directory.");
        ManagedPath.RejectRedirects(root);
        Directory.CreateDirectory(root);
        // Reject symlinks/junctions in every parent, preventing cleanup from crossing a redirected root.
        for (var current = new DirectoryInfo(root); current is not null; current = current.Parent)
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Cache cannot use redirected directories.");
    }
    [GeneratedRegex(@"^capture-[a-f0-9]{32}\.png\z", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
    [GeneratedRegex(@"^(capture-[a-f0-9]{32}\.png|history(?:-recovered)?\.json|history-page-[a-f0-9]{64}\.json)\.[a-f0-9]{32}\.tmp\z", RegexOptions.CultureInvariant)]
    private static partial Regex TemporaryPattern();
    public static bool IsSafeName(string? name) => name is not null && NamePattern().IsMatch(name);
    public string PathFor(CaptureRecord capture) => PathForName(capture.FileName);
    public string PathForName(string name)
    {
        if (!IsSafeName(name)) throw new InvalidDataException("Invalid capture filename.");
        var path = Path.Combine(root, name);
        ManagedPath.RejectRedirects(path);
        return path;
    }
    private bool SafeFile(string path) => File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
    private RepositoryState ReadState(string metadata)
    {
        if (!SafeFile(metadata)) throw new InvalidDataException("Invalid metadata location.");
        if (new FileInfo(metadata).Length > 4 * 1024 * 1024) throw new InvalidDataException("History too large.");
        var loaded = JsonSerializer.Deserialize<RepositoryState>(File.ReadAllText(metadata)) ?? throw new InvalidDataException("Empty history state.");
        if (loaded.SchemaVersion is < 1 or > 2) throw new InvalidDataException("Unsupported history version.");
        if (loaded.Captures is null || loaded.Superseded is null || loaded.Pages is null || loaded.Pages.Count > 10000) throw new InvalidDataException("Invalid history pages.");
        foreach (var page in loaded.Pages)
        {
            if (!PagePattern().IsMatch(page)) throw new InvalidDataException("Invalid history page name.");
            var path = Path.Combine(root, page);
            if (!SafeFile(path) || new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Invalid history page.");
            var bytes = File.ReadAllBytes(path);
            if ("history-page-" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + ".json" != page) throw new InvalidDataException("History page checksum mismatch.");
            loaded.Captures.AddRange(JsonSerializer.Deserialize<List<CaptureRecord>>(bytes) ?? throw new InvalidDataException("Empty history page."));
        }
        if (loaded.Captures is null || loaded.ProtectedUntil is null || loaded.Captures.Any(c => c is null)) throw new InvalidDataException("Invalid history state.");
        loaded.Captures = loaded.Captures.Where(c => IsSafeName(c.FileName) && c.Width > 0 && c.Height > 0 && SafeFile(PathFor(c)))
            .DistinctBy(c => c.Id).DistinctBy(c => c.FileName).ToList();
        loaded.Superseded = loaded.Superseded.Where(p => IsSafeName(p.Key) && SafeFile(PathForName(p.Key))).ToDictionary();
        loaded.ProtectedUntil = loaded.ProtectedUntil.Where(p => IsSafeName(p.Key) && p.Value > clock()).ToDictionary();
        return loaded;
    }
    public void Load()
    {
        ManagedPath.RejectRedirects(root);
        state = new(); Recovered = false; CleanupBlocked = false; recoveryNeedsBackup = false; pageCache.Clear(); committedPages.Clear();
        var metadata = Path.Combine(root, "history.json");
        var recovery = Path.Combine(root, "history-recovered.json");
        try
        {
            if (File.Exists(metadata)) { state = ReadState(metadata); committedPages = state.Pages.ToHashSet(); }
            else if (File.Exists(recovery)) throw new InvalidDataException("Unconfirmed recovered history.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
        {
            Recovered = true; CleanupBlocked = true;
            if (File.Exists(recovery))
                try { state = ReadState(recovery); }
                catch (Exception recoveryError) when (recoveryError is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
                { state = new(); recoveryNeedsBackup = true; }
        }
        var known = state.Captures.Select(c => c.FileName).Concat(state.Superseded.Keys).Concat(state.ProtectedUntil.Keys).ToHashSet();
        var files = Directory.EnumerateFiles(root, "capture-*.png");
        // During recovery every unknown image may be an old pin, including with history disabled.
        foreach (var path in files)
        {
            var name = Path.GetFileName(path);
            if (!IsSafeName(name) || !SafeFile(path) || known.Contains(name)) continue;
            // Recovery is metadata-only; the UI validates dimensions when decoding, with a pixel limit.
            if (HistoryEnabled || CleanupBlocked) state.Captures.Add(new() { FileName = name, CreatedUtc = File.GetLastWriteTimeUtc(path), Width = 1, Height = 1, DimensionsPending = true, Dismissed = true, Pinned = CleanupBlocked });
        }
    }
    [GeneratedRegex(@"^history-page-[a-f0-9]{64}\.json\z", RegexOptions.CultureInvariant)]
    private static partial Regex PagePattern();
    public void ResolveDimensions(CaptureRecord record)
    {
        if (!record.DimensionsPending) return;
        using var stream = File.OpenRead(PathFor(record));
        Span<byte> header = stackalloc byte[24]; stream.ReadExactly(header);
        if (!header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || !header.Slice(12, 4).SequenceEqual("IHDR"u8)) throw new InvalidDataException("Invalid recovered PNG.");
        var width = BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4)); var height = BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4));
        if (width <= 0 || height <= 0 || (long)width * height > 80_000_000) throw new InvalidDataException("Recovered PNG dimensions are invalid.");
        record.Width = width; record.Height = height; record.DimensionsPending = false;
    }
    public CaptureRecord Add(byte[] png, int width, int height, string monitor = "")
    {
        var record = new CaptureRecord { CreatedUtc = clock(), FileName = "capture-" + Guid.NewGuid().ToString("N") + ".png", Width = width, Height = height, Monitor = monitor };
        write(PathFor(record), png);
        sessionFiles.Add(record.FileName);
        state.Captures.Insert(0, record);
        try { Persist(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { PersistenceFailed?.Invoke(); }
        return record;
    }
    public void SetPinned(CaptureRecord record, bool pinned)
    {
        var previous = record.Pinned; record.Pinned = pinned;
        try { Persist(); } catch { record.Pinned = previous; throw; }
    }
    public void SetPinLayout(CaptureRecord record, PinLayout layout)
    {
        if (!state.Captures.Contains(record)) throw new InvalidOperationException("Capture is no longer available.");
        var previous = record.PinLayout; record.PinLayout = layout.Normalize();
        try { Persist(); } catch { record.PinLayout = previous; throw; }
    }
    public void Dismiss(IEnumerable<CaptureRecord> records)
    {
        var previous = records.DistinctBy(c => c.Id).Select(c => (c, c.Dismissed)).ToArray();
        foreach (var (record, _) in previous) record.Dismissed = true;
        try { Persist(); } catch { foreach (var (record, dismissed) in previous) record.Dismissed = dismissed; throw; }
    }
    public void Restore(IEnumerable<CaptureRecord> records, DateTimeOffset now)
    {
        var items = records.DistinctBy(c => c.Id).ToArray();
        if (items.Any(c => !state.Captures.Contains(c) || !SafeFile(PathFor(c)))) throw new IOException("Capture is no longer available.");
        var previous = items.Select(c => (c, c.Dismissed, c.RestoredUtc)).ToArray();
        foreach (var c in items) { c.Dismissed = false; c.RestoredUtc = now; }
        try { Persist(); }
        catch { foreach (var (c, dismissed, restored) in previous) { c.Dismissed = dismissed; c.RestoredUtc = restored; } throw; }
    }
    public void Replace(CaptureRecord record, byte[] png, int width, int height, string? expectedRevision = null)
    {
        if (!state.Captures.Contains(record) || (expectedRevision is not null && record.FileName != expectedRevision)) throw new InvalidOperationException("Capture changed in another view. Reopen the editor before applying changes.");
        // Immutable file identity keeps existing receiver/clipboard payloads intact after editing.
        var name = "capture-" + Guid.NewGuid().ToString("N") + ".png";
        var revisionPath = PathForName(name);
        write(revisionPath, png);
        sessionFiles.Add(name);
        var previous = (record.FileName, record.Width, record.Height, record.Edited, record.DimensionsPending);
        state.Superseded[record.FileName] = clock();
        record.FileName = name; record.Width = width; record.Height = height; record.Edited = true; record.DimensionsPending = false;
        try { PersistState(); }
        catch
        {
            state.Superseded.Remove(previous.FileName);
            (record.FileName, record.Width, record.Height, record.Edited, record.DimensionsPending) = previous;
            // This revision was never committed or published to views/transfers. Do not recover
            // its failed edit as a separate capture on the next launch.
            try { if (SafeFile(revisionPath)) File.Delete(revisionPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
        RevisionChanged?.Invoke(record);
        Changed?.Invoke();
    }
    public void Persist()
    {
        PersistState();
        Changed?.Invoke();
    }
    private void PersistState()
    {
        ManagedPath.RejectRedirects(root);
        try
        {
            var metadata = Path.Combine(root, CleanupBlocked ? "history-recovered.json" : "history.json");
            if (File.Exists(metadata) && !SafeFile(metadata)) throw new InvalidDataException("Invalid metadata location.");
            if (recoveryNeedsBackup)
            {
                File.Copy(metadata, Path.Combine(root, "history-recovered.invalid-" + Guid.NewGuid().ToString("N") + ".json"));
                recoveryNeedsBackup = false;
            }
            var serialized = SerializeState(out var pages);
            write(metadata, serialized); committedPages = pages; PersistencePending = false;
        }
        catch { PersistencePending = true; throw; }
    }
    public void ProbeWritable()
    {
        var path = Path.Combine(root, ".write-probe-" + Guid.NewGuid().ToString("N"));
        try { write(path, [0]); }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
    private byte[] SerializeState() => SerializeState(out _);
    private byte[] SerializeState(out HashSet<string> pages)
    {
        var saved = new RepositoryState
        {
            Captures = state.Captures.Where(c => HistoryEnabled || c.Pinned).ToList(),
            Superseded = state.Superseded,
            ProtectedUntil = state.ProtectedUntil
        };
        if (!CleanupBlocked && saved.Captures.Count > 512)
        {
            saved.SchemaVersion = 2; // Older builds fail closed instead of ignoring pinned page records.
            var captures = saved.Captures;
            // Keep the changing head in the atomic manifest. Only sealed older chunks need
            // immutable page files, avoiding a new abandoned file for every capture.
            var first = captures.Count % 512; if (first == 0) first = 512;
            saved.Captures = captures.GetRange(0, first);
            var activeCache = new HashSet<Guid>();
            for (var offset = first; offset < captures.Count; offset += 512)
            {
                var key = captures[offset].Id; activeCache.Add(key);
                var cached = pageCache.GetValueOrDefault(key);
                var unchanged = cached is not null && cached.Stamps.Length == 512;
                for (var i = 0; unchanged && i < 512; i++) unchanged = cached!.Stamps[i] == CaptureStamp.From(captures[offset + i]);
                byte[]? page = null;
                if (!unchanged)
                {
                    page = JsonSerializer.SerializeToUtf8Bytes(captures.GetRange(offset, 512));
                    if (page.Length > 4 * 1024 * 1024) throw new InvalidDataException("History page exceeds the size limit.");
                    cached = new("history-page-" + Convert.ToHexString(SHA256.HashData(page)).ToLowerInvariant() + ".json",
                        Enumerable.Range(offset, 512).Select(i => CaptureStamp.From(captures[i])).ToArray());
                }
                var name = cached!.Name;
                var path = Path.Combine(root, name);
                if (File.Exists(path) && !SafeFile(path)) throw new InvalidDataException("Invalid history page location.");
                if (!SafeFile(path)) write(path, page ?? JsonSerializer.SerializeToUtf8Bytes(captures.GetRange(offset, 512)));
                pageCache[key] = cached; saved.Pages.Add(name);
            }
            foreach (var key in pageCache.Keys.Where(k => !activeCache.Contains(k)).ToArray()) pageCache.Remove(key);
        }
        else pageCache.Clear();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(saved);
        if (bytes.Length > 4 * 1024 * 1024) throw new InvalidDataException("History exceeds the recovery size limit. Metadata has not been replaced; cleanup remains blocked during recovery.");
        pages = saved.Pages.ToHashSet();
        return bytes;
    }
    public void ConfirmHistoryRecovery()
    {
        if (!CleanupBlocked) return;
        // Persist the reviewed state first so a failed promotion can be retried after restart.
        Persist();
        var metadata = Path.Combine(root, "history.json");
        if (File.Exists(metadata))
        {
            if (!SafeFile(metadata)) throw new InvalidDataException("Invalid metadata location.");
            File.Copy(metadata, Path.Combine(root, "history.invalid-" + Guid.NewGuid().ToString("N") + ".json"));
        }
        write(metadata, SerializeState());
        CleanupBlocked = false;
    }
    public IDisposable Lease(IEnumerable<CaptureRecord> records, bool transfer = false)
    {
        var names = records.Select(c => c.FileName).Distinct().ToArray();
        foreach (var name in names) PathForName(name);
        var previous = names.ToDictionary(name => name, name => state.ProtectedUntil.GetValueOrDefault(name));
        foreach (var name in names)
        {
            leases[name] = leases.GetValueOrDefault(name) + 1;
            if (transfer) ExtendTransferGrace(name);
        }
        void Release() { foreach (var name in names) { var count = leases.GetValueOrDefault(name); if (count <= 1) leases.Remove(name); else leases[name] = count - 1; } }
        if (transfer)
            try { Persist(); }
            catch
            {
                Release();
                foreach (var name in names) { if (previous[name] == default) state.ProtectedUntil.Remove(name); else state.ProtectedUntil[name] = previous[name]; }
                throw;
            }
        return new ReleaseLease(() =>
        {
            Release();
            if (!transfer) return;
            foreach (var name in names) ExtendTransferGrace(name);
            try { Persist(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { PersistenceFailed?.Invoke(); }
        });
    }
    private void ExtendTransferGrace(string name)
    {
        var deadline = clock().AddHours(24);
        // A new transfer or release must preserve an already promised durable deadline,
        // including when Windows corrects its clock backwards while the app is running.
        if (deadline > state.ProtectedUntil.GetValueOrDefault(name)) state.ProtectedUntil[name] = deadline;
    }
    public int CleanupSession(DateTimeOffset now) => Cleanup(now, -1, clear: true, sessionOnly: true);
    public int Cleanup(DateTimeOffset now, int retentionHours, bool clear = false, bool sessionOnly = false)
        => CleanupSteps(now, retentionHours, clear, sessionOnly).Sum();
    public async Task<int> CleanupAsync(DateTimeOffset now, int retentionHours, bool clear = false, bool sessionOnly = false,
        Func<Task>? yield = null, CancellationToken cancellation = default, int batchSize = 16)
    {
        if (batchSize is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(batchSize));
        await cleanupSerial.WaitAsync(cancellation);
        var removed = 0; var finished = false;
        try
        {
            // State/lease decisions remain on the caller's context. Yield between bounded
            // slices and recheck protections, rather than deleting from a stale worker snapshot.
            var slice = Stopwatch.StartNew(); var count = 0;
            foreach (var item in CleanupSteps(now, retentionHours, clear, sessionOnly))
            {
                removed += item;
                cancellation.ThrowIfCancellationRequested();
                if (++count < batchSize && slice.ElapsedMilliseconds < 6) continue;
                cancellation.ThrowIfCancellationRequested();
                if (yield is null) await Task.Yield(); else await yield();
                cancellation.ThrowIfCancellationRequested();
                count = 0; slice.Restart();
            }
            finished = true; return removed;
        }
        finally
        {
            try { if (!finished && removed > 0 && !CleanupBlocked && !PersistencePending) Persist(); }
            finally { cleanupSerial.Release(); }
        }
    }
    private IEnumerable<int> CleanupSteps(DateTimeOffset now, int retentionHours, bool clear, bool sessionOnly)
    {
        ManagedPath.RejectRedirects(root);
        if (CleanupBlocked || PersistencePending) yield break; // Corrupt pin metadata must never turn into permission to delete images.
        var records = state.Captures.ToDictionary(c => c.FileName);
        foreach (var path in Directory.GetFiles(root, "capture-*.png"))
        {
            yield return 0;
            if (CleanupBlocked || PersistencePending) yield break;
            var name = Path.GetFileName(path);
            var record = records.GetValueOrDefault(name);
            if ((sessionOnly && !sessionFiles.Contains(name)) || !IsSafeName(name) || !SafeFile(path) || record?.Pinned == true || leases.ContainsKey(name) || state.ProtectedUntil.GetValueOrDefault(name) > now) continue;
            var created = record is null ? new DateTimeOffset(File.GetLastWriteTimeUtc(path)) : record.RestoredUtc ?? record.CreatedUtc;
            if (!clear && (retentionHours < 0 || created.AddHours(retentionHours) > now)) continue;
            var deleted = false;
            try { File.Delete(path); state.Superseded.Remove(name); state.Captures.RemoveAll(c => c.FileName == name); deleted = true; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            if (deleted) yield return 1;
        }
        foreach (var key in state.ProtectedUntil.Where(p => p.Value <= now).Select(p => p.Key).ToArray())
            if (state.ProtectedUntil.GetValueOrDefault(key) <= now) state.ProtectedUntil.Remove(key);
        // Remove only old, owned incomplete atomic writes, never unknown files.
        foreach (var path in Directory.EnumerateFiles(root, "*.tmp"))
        {
            yield return 0;
            if (CleanupBlocked || PersistencePending) yield break;
            if (SafeFile(path) && File.GetLastWriteTimeUtc(path) < now.UtcDateTime.AddDays(-1) &&
                TemporaryPattern().IsMatch(Path.GetFileName(path)))
                try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        Persist();
        // Only owned pages unreferenced by the durable manifest and older than the crash grace are removable.
        var hasArchivedHistory = Directory.EnumerateFiles(root, "history.invalid-*.json").Any();
        foreach (var path in hasArchivedHistory ? Enumerable.Empty<string>() : Directory.EnumerateFiles(root, "history-page-*.json"))
        {
            yield return 0;
            if (CleanupBlocked || PersistencePending) yield break;
            if (PagePattern().IsMatch(Path.GetFileName(path)) && !committedPages.Contains(Path.GetFileName(path)) && SafeFile(path) && File.GetLastWriteTimeUtc(path) < now.UtcDateTime.AddDays(-1))
                try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    private sealed class ReleaseLease(Action release) : IDisposable
    {
        private Action? action = release;
        public void Dispose() => Interlocked.Exchange(ref action, null)?.Invoke();
    }
}

public static class TransferPayload
{
    public static IReadOnlyList<CaptureRecord> Ordered(CaptureRepository repository, IEnumerable<CaptureRecord> records)
    {
        var requested = records.ToArray(); var ids = requested.Select(c => c.Id).ToHashSet();
        var ordered = repository.Captures.Where(c => ids.Contains(c.Id)).ToArray();
        var current = ordered.ToDictionary(c => c.Id, c => c.FileName);
        if (ordered.Length == 0 || ordered.Length != ids.Count || requested.Any(c => current[c.Id] != c.FileName))
            throw new IOException("Selected capture is no longer available in the shelf. Select it again.");
        return ordered;
    }
    public static string[] Files(CaptureRepository repository, IEnumerable<CaptureRecord> records)
    {
        var paths = Ordered(repository, records).Select(repository.PathFor).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length == 0 || paths.Any(p => !File.Exists(p))) throw new IOException("Capture file is no longer available.");
        return paths;
    }
}
public static class StartupCommand
{
    public static bool Matches(string? command, string executable) => string.Equals(command, Build(executable), StringComparison.OrdinalIgnoreCase);
    public static string Build(string executable)
    {
        if (!Path.IsPathFullyQualified(executable) || executable.Contains('"') || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Startup executable must be an absolute executable path.");
        return $"\"{executable}\" --background";
    }
}

public static class CaptureLifetime
{
    public static bool Visible(CaptureRecord capture, int minutes, DateTimeOffset now) =>
        !capture.Dismissed && (capture.Pinned || minutes == 0 || (capture.RestoredUtc ?? capture.CreatedUtc).AddMinutes(minutes) > now);
}
