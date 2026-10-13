using System.Runtime.InteropServices;

namespace SnippyGrab.App.Services;

internal sealed class ClipboardService(Action<DataObject>? write = null, Func<int, Task>? delay = null,
    Func<string, bool, CancellationToken, (BitmapSource Image, byte[]? Png)>? prepareStored = null)
{
    private readonly ClipboardWriter writer = new();
    private readonly LatestOperation preparation = new();
    private readonly SemaphoreSlim serial = new(1);
    public void Invalidate() { preparation.Cancel(); writer.Invalidate(); }
    public async Task<bool> ImageAsync(BitmapSource image, bool png, byte[]? encoded = null, CancellationToken cancellation = default)
    {
        var operation = writer.BeginOperation();
        var token = preparation.Begin(cancellation);
        try
        {
            if (!image.IsFrozen) { image = image.CloneCurrentValue(); image.Freeze(); }
            await serial.WaitAsync(token);
            try
            {
                if (png && encoded is null)
                {
                    encoded = await ImageWorker.RunAsync(() => ImageService.Png(image), token);
                }
                token.ThrowIfCancellationRequested();
                if (png && encoded is not null) ImageLimits.ValidateEncoded(encoded.Length);
                return await PublishImageAsync(image, png ? encoded : null, token, operation);
            }
            finally { serial.Release(); }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return false; }
    }
    internal async Task<bool> StoredImageAsync(string path, bool png, CancellationToken cancellation = default)
    {
        // Reserve before yielding: a slower decode must never overwrite a newer copy/OCR action.
        var operation = writer.BeginOperation();
        var token = preparation.Begin(cancellation);
        try
        {
            await serial.WaitAsync(token);
            try
            {
                var prepared = await ImageWorker.RunAsync(() =>
                {
                    token.ThrowIfCancellationRequested();
                    if (prepareStored is not null) return prepareStored(path, png, token);
                    ManagedPath.RejectRedirects(path);
                    return (Image: ImageService.Load(path), Png: png ? ImageService.ReadEncoded(path) : null);
                }, token);
                token.ThrowIfCancellationRequested();
                return await PublishImageAsync(prepared.Image, prepared.Png, token, operation);
            }
            finally { serial.Release(); }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return false; }
    }
    private Task<bool> PublishImageAsync(BitmapSource image, byte[]? png, CancellationToken cancellation, int operation)
    {
        var data = new DataObject(); data.SetImage(image);
        if (png is not null) data.SetData("PNG", new MemoryStream(png, writable: false));
        return SetAsync(data, cancellation, operation);
    }
    public Task<bool> TextAsync(string text, CancellationToken cancellation = default) { preparation.Cancel(); return SetAsync(new DataObject(DataFormats.UnicodeText, text), cancellation); }
    public Task<bool> FilesAsync(string[] paths) { preparation.Cancel(); return SetAsync(new DataObject(DataFormats.FileDrop, paths)); }
    private Task<bool> SetAsync(DataObject data, CancellationToken cancellation = default, int? operation = null) =>
        writer.WriteAsync(() => { if (write is null) Clipboard.SetDataObject(data, true); else write(data); }, delay, cancellation, operation);

}

internal sealed class DragDropService(CaptureRepository repository)
{
    private bool preparing;
    internal async Task DragAsync(DependencyObject source, IReadOnlyList<CaptureRecord> records)
    {
        if (preparing) return; preparing = true;
        try
        {
            var ordered = TransferPayload.Ordered(repository, records);
            using var pending = repository.Lease(ordered);
            var revisions = ordered.Select(c => c.FileName).ToArray();
            await using var lease = await repository.TransferLeaseAsync(ordered);
            if (!ordered.Select(c => c.FileName).SequenceEqual(revisions)) throw new InvalidOperationException("Capture changed while preparing the drag. Retry dragging.");
            var paths = TransferPayload.Files(repository, ordered);
            (BitmapSource Image, byte[] Png)? prepared = ordered.Count == 1 ? await ImageWorker.RunAsync(() => (ImageService.Load(paths[0]), ImageService.ReadEncoded(paths[0]))) : null;
            if (source is FrameworkElement { IsLoaded: false }) return;
            var data = new DataObject(); data.SetData(DataFormats.FileDrop, paths);
            if (prepared is { } image) { data.SetImage(image.Image); data.SetData("PNG", new MemoryStream(image.Png, writable: false)); }
            DragDrop.DoDragDrop(source, data, DragDropEffects.Copy);
        }
        finally { preparing = false; }
    }
    public void Drag(DependencyObject source, IReadOnlyList<CaptureRecord> records)
    {
        var ordered = TransferPayload.Ordered(repository, records);
        using var lease = repository.Lease(ordered, transfer: true);
        DragDrop.DoDragDrop(source, BuildData(ordered), DragDropEffects.Copy);
    }
    internal DataObject BuildData(IReadOnlyList<CaptureRecord> records)
    {
        var ordered = TransferPayload.Ordered(repository, records);
        var paths = TransferPayload.Files(repository, ordered);
        var data = new DataObject(); data.SetData(DataFormats.FileDrop, paths);
        if (ordered.Count == 1)
        {
            var image = ImageService.Load(paths[0]); data.SetImage(image);
            // Managed revisions are already lossless PNGs. Keep the same bytes as the
            // file-drop payload instead of encoding the full image again on the UI thread.
            data.SetData("PNG", new MemoryStream(File.ReadAllBytes(paths[0])));
        }
        return data;
    }
}
