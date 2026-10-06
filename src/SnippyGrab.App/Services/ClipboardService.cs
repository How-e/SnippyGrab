using System.Runtime.InteropServices;

namespace SnippyGrab.App.Services;

internal sealed class ClipboardService
{
    private int generation;
    public async Task<bool> ImageAsync(BitmapSource image, bool png, byte[]? encoded = null)
    {
        var data = new DataObject(); data.SetImage(image);
        if (png) data.SetData("PNG", new MemoryStream(encoded ?? ImageService.Png(image)));
        return await SetAsync(data);
    }
    public Task<bool> TextAsync(string text) => SetAsync(new DataObject(DataFormats.UnicodeText, text));
    public Task<bool> FilesAsync(string[] paths) => SetAsync(new DataObject(DataFormats.FileDrop, paths));
    private async Task<bool> SetAsync(DataObject data)
    {
        var operation = ++generation;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (operation != generation) return false;
            try { Clipboard.SetDataObject(data, true); return true; }
            catch (ExternalException) { await Task.Delay(25 * (attempt + 1)); }
        }
        return false;
    }
}

internal sealed class DragDropService(CaptureRepository repository)
{
    public void Drag(DependencyObject source, IReadOnlyList<CaptureRecord> records)
    {
        using var lease = repository.Lease(records, transfer: true);
        var paths = TransferPayload.Files(repository, records);
        var data = new DataObject(); data.SetData(DataFormats.FileDrop, paths);
        if (records.Count == 1)
        {
            var image = ImageService.Load(paths[0]); data.SetImage(image);
            data.SetData("PNG", new MemoryStream(ImageService.Png(image)));
        }
        DragDrop.DoDragDrop(source, data, DragDropEffects.Copy);
    }
}
