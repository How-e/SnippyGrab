namespace SnippyGrab.App.Services;

// One running decode and at most one current queued request per view. Cancellation
// prevents older selections or revisions from replacing a newer preview.
internal sealed class PreviewLoader(Func<string, int, int, BitmapSource>? decode = null) : IDisposable
{
    private readonly LatestOperation latest = new();
    private readonly SemaphoreSlim serial = new(1);
    private bool disposed;
    public async Task<BitmapSource> LoadAsync(string path, int width, int height = 0)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var token = latest.Begin();
        await serial.WaitAsync(token);
        try
        {
            var image = await Task.Run(() => (decode ?? ImageService.LoadPreview)(path, width, height), token);
            token.ThrowIfCancellationRequested(); return image;
        }
        finally { serial.Release(); }
    }
    public void Cancel() => latest.Cancel();
    internal async Task DrainAsync() { await serial.WaitAsync(); serial.Release(); }
    public void Dispose() { disposed = true; latest.Dispose(); }
}
