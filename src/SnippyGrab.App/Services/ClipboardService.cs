using System.Runtime.InteropServices;

namespace SnippyGrab.App.Services;

internal sealed class ClipboardService(Action<DataObject>? write = null, Func<int, Task>? delay = null)
{
    private readonly ClipboardWriter writer = new();
    public void Invalidate() => writer.Invalidate();
    public async Task<bool> ImageAsync(BitmapSource image, bool png, byte[]? encoded = null, CancellationToken cancellation = default)
    {
        var operation = writer.BeginOperation();
        try
        {
            if (png && encoded is null)
            {
                if (!image.IsFrozen) { image = image.CloneCurrentValue(); image.Freeze(); }
                encoded = await Task.Run(() => ImageService.Png(image), cancellation);
            }
            return await PublishImageAsync(image, png ? encoded : null, cancellation, operation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return false; }
    }
    internal async Task<bool> StoredImageAsync(string path, bool png, CancellationToken cancellation = default)
    {
        // Reserve before yielding: a slower decode must never overwrite a newer copy/OCR action.
        var operation = writer.BeginOperation();
        try
        {
            var prepared = await Task.Run(() =>
            {
                cancellation.ThrowIfCancellationRequested(); ManagedPath.RejectRedirects(path);
                return (Image: ImageService.Load(path), Png: png ? ImageService.ReadEncoded(path) : null);
            }, cancellation);
            return await PublishImageAsync(prepared.Image, prepared.Png, cancellation, operation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return false; }
    }
    private Task<bool> PublishImageAsync(BitmapSource image, byte[]? png, CancellationToken cancellation, int operation)
    {
        var data = new DataObject(); data.SetImage(image);
        if (png is not null) data.SetData("PNG", new MemoryStream(png, writable: false));
        return SetAsync(data, cancellation, operation);
    }
    public Task<bool> TextAsync(string text, CancellationToken cancellation = default) => SetAsync(new DataObject(DataFormats.UnicodeText, text), cancellation);
    public Task<bool> FilesAsync(string[] paths) => SetAsync(new DataObject(DataFormats.FileDrop, paths));
    private Task<bool> SetAsync(DataObject data, CancellationToken cancellation = default, int? operation = null) =>
        writer.WriteAsync(() => { if (write is null) Clipboard.SetDataObject(data, true); else write(data); }, delay, cancellation, operation);

}

internal sealed class DragDropService(CaptureRepository repository)
{
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
