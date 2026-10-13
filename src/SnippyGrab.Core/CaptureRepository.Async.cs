namespace SnippyGrab.Core;

public sealed partial class CaptureRepository
{
    // Mutations resume on the caller's context; only immutable metadata snapshots
    // and file writes go to workers. Do not move lease/pin decisions off that context.
    private async Task<bool> PersistSnapshotAsync(RepositoryState? snapshot = null)
    {
        var version = Interlocked.Increment(ref persistenceVersion);
        snapshot ??= SnapshotState();
        await Task.Run(async () =>
        {
            await storageSerial.WaitAsync().ConfigureAwait(false);
            try { PersistStateCore(snapshot, version); }
            finally { storageSerial.Release(); }
        });
        return writtenVersion == version;
    }
    public async Task PersistAsync()
    {
        await mutationSerial.WaitAsync();
        try { await PersistSnapshotAsync(); Changed?.Invoke(); }
        finally { mutationSerial.Release(); }
    }
    public async Task DrainAsync() { await mutationSerial.WaitAsync(); mutationSerial.Release(); }
    public async Task ConfirmHistoryRecoveryAsync()
    {
        await mutationSerial.WaitAsync();
        try
        {
            if (!CleanupBlocked) return;
            await PersistSnapshotAsync();
            var snapshot = SnapshotState();
            await Task.Run(async () =>
            {
                await storageSerial.WaitAsync().ConfigureAwait(false);
                try
                {
                    var metadata = Path.Combine(root, "history.json");
                    ManagedPath.RejectRedirects(metadata);
                    if (File.Exists(metadata)) { if (!SafeFile(metadata)) throw new InvalidDataException("Invalid metadata location."); File.Copy(metadata, Path.Combine(root, "history.invalid-" + Guid.NewGuid().ToString("N") + ".json")); }
                    write(metadata, SerializeState(out var pages, snapshot)); committedPages = pages;
                }
                finally { storageSerial.Release(); }
            });
            CleanupBlocked = false; Changed?.Invoke();
        }
        finally { mutationSerial.Release(); }
    }
    public async Task<CaptureRecord> AddAsync(byte[] png, int width, int height, string monitor = "")
    {
        ImageLimits.ValidateEncoded(png.Length); if (width <= 0 || height <= 0) throw new InvalidDataException("Invalid image dimensions."); ImageLimits.Validate((long)width * height);
        await mutationSerial.WaitAsync();
        var record = new CaptureRecord { CreatedUtc = clock(), FileName = "capture-" + Guid.NewGuid().ToString("N") + ".png", Width = width, Height = height, Monitor = monitor };
        IDisposable? pending = null;
        try
        {
            pending = Lease([record]);
            var path = PathFor(record); await Task.Run(() => write(path, png));
            sessionFiles.Add(record.FileName); state.Captures.Insert(0, record);
            try { await PersistSnapshotAsync(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { PersistenceFailed?.Invoke(); }
            Changed?.Invoke(); return record;
        }
        finally { pending?.Dispose(); mutationSerial.Release(); }
    }
    public async Task ReplaceAsync(CaptureRecord record, byte[] png, int width, int height, string? expectedRevision = null)
    {
        ImageLimits.ValidateEncoded(png.Length); if (width <= 0 || height <= 0) throw new InvalidDataException("Invalid image dimensions."); ImageLimits.Validate((long)width * height);
        await mutationSerial.WaitAsync();
        var name = "capture-" + Guid.NewGuid().ToString("N") + ".png";
        var pendingRecord = new CaptureRecord { FileName = name }; IDisposable? pending = null;
        string? path = null;
        var committed = false;
        try
        {
            pending = Lease([pendingRecord]); path = PathForName(name);
            if (!state.Captures.Contains(record) || (expectedRevision is not null && record.FileName != expectedRevision)) throw new InvalidOperationException("Capture changed in another view. Reopen the editor before applying changes.");
            var prior = record.FileName;
            await Task.Run(() => write(path, png));
            // Never publish an uncommitted revision to a copy/editor/pin while
            // awaiting disk. Older revision leases remain intact until commit.
            while (true)
            {
                if (record.FileName != prior) throw new InvalidOperationException("Capture changed before the edit could be committed.");
                var snapshot = SnapshotState();
                var replacement = snapshot.Captures.FirstOrDefault(c => c.Id == record.Id);
                if (replacement is not null) { replacement.FileName = name; replacement.Width = width; replacement.Height = height; replacement.Edited = true; replacement.DimensionsPending = false; }
                snapshot.Superseded[prior] = clock();
                if (!await PersistSnapshotAsync(snapshot)) continue;
                state.Superseded[prior] = snapshot.Superseded[prior]; break;
            }
            sessionFiles.Add(name); committed = true;
            record.FileName = name; record.Width = width; record.Height = height; record.Edited = true; record.DimensionsPending = false;
            RevisionChanged?.Invoke(record); Changed?.Invoke();
        }
        catch
        {
            if (!committed && path is not null) await Task.Run(() => { try { if (SafeFile(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } });
            throw;
        }
        finally { pending?.Dispose(); mutationSerial.Release(); }
    }
    private async Task ChangeAsync(Action change, Action rollback)
    {
        await mutationSerial.WaitAsync();
        try
        {
            change();
            try { await PersistSnapshotAsync(); }
            catch { rollback(); throw; }
            Changed?.Invoke();
        }
        finally { mutationSerial.Release(); }
    }
    public Task SetPinnedAsync(CaptureRecord record, bool pinned)
    {
        var prior = false;
        return ChangeAsync(() => { prior = record.Pinned; record.Pinned = pinned; }, () => record.Pinned = prior);
    }
    public Task SetPinLayoutAsync(CaptureRecord record, PinLayout layout)
    {
        PinLayout? prior = null;
        return ChangeAsync(() => { if (!state.Captures.Contains(record)) throw new InvalidOperationException("Capture is no longer available."); prior = record.PinLayout; record.PinLayout = layout.Normalize(); }, () => record.PinLayout = prior);
    }
    public Task DismissAsync(IEnumerable<CaptureRecord> records)
    {
        var items = records.DistinctBy(c => c.Id).ToArray(); (CaptureRecord Record, bool Dismissed)[] prior = [];
        return ChangeAsync(() => { prior = items.Select(c => (c, c.Dismissed)).ToArray(); foreach (var c in items) c.Dismissed = true; }, () => { foreach (var (c, dismissed) in prior) c.Dismissed = dismissed; });
    }
    public Task RestoreAsync(IEnumerable<CaptureRecord> records, DateTimeOffset now)
    {
        var items = records.DistinctBy(c => c.Id).ToArray(); (CaptureRecord Record, bool Dismissed, DateTimeOffset? Restored)[] prior = [];
        return ChangeAsync(() =>
        {
            if (items.Any(c => !state.Captures.Contains(c) || !SafeFile(PathFor(c)))) throw new IOException("Capture is no longer available.");
            prior = items.Select(c => (c, c.Dismissed, c.RestoredUtc)).ToArray(); foreach (var c in items) { c.Dismissed = false; c.RestoredUtc = now; }
        }, () => { foreach (var (c, dismissed, restored) in prior) { c.Dismissed = dismissed; c.RestoredUtc = restored; } });
    }
    public Task ReorderAsync(Guid from, Guid target)
    {
        CaptureRecord[] prior = [];
        return ChangeAsync(() => { prior = state.Captures.ToArray(); ShelfOrder.Move(state.Captures, from, target); }, () => { state.Captures.Clear(); state.Captures.AddRange(prior); });
    }
    public async Task RememberExportAsync(CaptureRecord record, string path, string? expectedRevision = null)
    {
        var revision = expectedRevision ?? record.FileName;
        await mutationSerial.WaitAsync();
        try
        {
            if (!state.Captures.Contains(record) || record.FileName != revision) throw new InvalidOperationException("Capture changed before export history could be updated.");
            record.Saved = true; record.ExportPath = path; await PersistSnapshotAsync(); Changed?.Invoke();
        }
        finally { mutationSerial.Release(); }
    }
    public async Task<IAsyncDisposable> TransferLeaseAsync(IEnumerable<CaptureRecord> records)
    {
        var items = records.ToArray();
        await mutationSerial.WaitAsync();
        try
        {
            var names = items.Select(c => c.FileName).Distinct().ToArray();
            var prior = names.ToDictionary(n => n, n => state.ProtectedUntil.GetValueOrDefault(n));
            var lease = Lease(items);
            foreach (var name in names) ExtendTransferGrace(name);
            try { await PersistSnapshotAsync(); Changed?.Invoke(); }
            catch
            {
                lease.Dispose(); foreach (var name in names) { if (prior[name] == default) state.ProtectedUntil.Remove(name); else state.ProtectedUntil[name] = prior[name]; }
                throw;
            }
            return new AsyncTransferLease(async () =>
            {
                await mutationSerial.WaitAsync();
                try
                {
                    foreach (var name in names) ExtendTransferGrace(name);
                    lease.Dispose();
                    try { await PersistSnapshotAsync(); Changed?.Invoke(); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { PersistenceFailed?.Invoke(); }
                }
                finally { mutationSerial.Release(); }
            });
        }
        finally { mutationSerial.Release(); }
    }
    private sealed class AsyncTransferLease(Func<Task> release) : IAsyncDisposable
    {
        private Func<Task>? action = release;
        public async ValueTask DisposeAsync() { var callback = Interlocked.Exchange(ref action, null); if (callback is not null) await callback(); }
    }
}
