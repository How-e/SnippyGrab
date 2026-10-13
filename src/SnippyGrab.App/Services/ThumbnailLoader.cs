namespace SnippyGrab.App.Services;

internal sealed class ThumbnailLoader(CaptureRepository repository, int capacity) : IDisposable
{
    private readonly BoundedCache<(string File, int Width, int Height), BitmapSource> cache = new(capacity);
    private readonly Dictionary<(string File, int Width, int Height), (CancellationTokenSource Cancel, Task<BitmapSource> Work)> pending = [];
    private readonly Dictionary<CancellationTokenSource, Task<BitmapSource>> operations = [];
    private bool disposed;
    internal int Count => cache.Count;
    internal int DecodeCount { get; private set; }
    internal bool TryGet(string file, int width, int height, out BitmapSource image) => cache.TryGetValue((file, width, height), out image);
    internal Task<BitmapSource> LoadAsync(CaptureRecord record, int width, int height)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var key = (record.FileName, width, height);
        if (cache.TryGetValue(key, out var found)) return Task.FromResult(found);
        if (pending.TryGetValue(key, out var existing)) return existing.Work;
        if (pending.Count >= capacity)
        {
            var oldest = pending.First(); pending.Remove(oldest.Key); oldest.Value.Cancel.Cancel();
        }
        var cancel = new CancellationTokenSource();
        var completion = new TaskCompletionSource<BitmapSource>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[key] = (cancel, completion.Task); operations[cancel] = completion.Task;
        _ = LoadCoreAsync(key, cancel, completion); return completion.Task;
    }
    private async Task LoadCoreAsync((string File, int Width, int Height) key, CancellationTokenSource cancel, TaskCompletionSource<BitmapSource> completion)
    {
        IDisposable? lease = null; var token = cancel.Token;
        try
        {
            lease = repository.Lease([new CaptureRecord { FileName = key.File }]);
            var path = repository.PathForName(key.File);
            var image = await ImageWorker.RunAsync(() => ImageService.LoadPreview(path, key.Width, key.Height), token);
            token.ThrowIfCancellationRequested();
            if (!disposed) { DecodeCount++; cache.GetOrAdd(key, () => image); }
            completion.TrySetResult(image);
        }
        catch (OperationCanceledException) { completion.TrySetCanceled(token); }
        catch (Exception ex) { completion.TrySetException(ex); }
        finally { lease?.Dispose(); if (pending.TryGetValue(key, out var current) && ReferenceEquals(current.Cancel, cancel)) pending.Remove(key); operations.Remove(cancel); cancel.Dispose(); }
    }
    internal void Prune(Func<string, bool> retain)
    {
        cache.RemoveWhere(key => !retain(key.File));
        foreach (var entry in pending.Where(p => !retain(p.Key.File)).ToArray()) { pending.Remove(entry.Key); entry.Value.Cancel.Cancel(); }
    }
    internal async Task DrainAsync()
    {
        foreach (var work in operations.Values.ToArray())
            try { await work; } catch (Exception ex) when (ex is OperationCanceledException or IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException) { }
    }
    internal void Clear() => Prune(_ => false);
    public void Dispose() { disposed = true; Clear(); }
}
