using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnippyGrab.App.Services;
using SnippyGrab.App.Views;
using SnippyGrab.Core;

namespace SnippyGrab.IntegrationTests;

public sealed class ImageIntegrationTests
{
    [Fact] public void EveryClipboardRepresentationReportsItsOwnOutcomeWithoutWritingOsClipboard()
    {
        Sta(() =>
        {
            var writes = new List<System.Windows.DataObject>(); bool busy = false; int attempts = 0;
            var clipboard = new ClipboardService(data => { attempts++; if (busy) throw new System.Runtime.InteropServices.ExternalException(); writes.Add(data); }, _ => Task.CompletedTask);
            Assert.True(clipboard.ImageAsync(Synthetic(), true).GetAwaiter().GetResult());
            Assert.True(writes[^1].GetDataPresent(System.Windows.DataFormats.Bitmap)); Assert.True(writes[^1].GetDataPresent("PNG"));
            Assert.True(clipboard.ImageAsync(Synthetic(), false).GetAwaiter().GetResult()); Assert.False(writes[^1].GetDataPresent("PNG", false));
            Assert.True(clipboard.FilesAsync([@"C:\synthetic.png"]).GetAwaiter().GetResult()); Assert.Equal(new[] { @"C:\synthetic.png" }, writes[^1].GetData(System.Windows.DataFormats.FileDrop));
            foreach (var text in new[] { "synthetic OCR text", @"C:\synthetic.png", "synthetic.png" })
            { Assert.True(clipboard.TextAsync(text).GetAwaiter().GetResult()); Assert.Equal(text, writes[^1].GetData(System.Windows.DataFormats.UnicodeText)); }
            busy = true;
            foreach (var action in new Func<Task<bool>>[] { () => clipboard.ImageAsync(Synthetic(), true), () => clipboard.FilesAsync([@"C:\synthetic.png"]), () => clipboard.TextAsync("synthetic") })
            { var before = attempts; Assert.False(action().GetAwaiter().GetResult()); Assert.Equal(6, attempts - before); }
            return true;
        });
    }
    internal static T Sta<T>(Func<T> action)
    {
        T result = default!; Exception? error = null;
        var thread = new Thread(() => { try { result = action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw(); return result;
    }
    private static BitmapSource Synthetic()
    {
        var pixels = new byte[160 * 100 * 4];
        for (var y = 0; y < 100; y++) for (var x = 0; x < 160; x++)
        { var offset = (y * 160 + x) * 4; pixels[offset] = (byte)(x % 2 == 0 ? 255 : 0); pixels[offset + 1] = 100; pixels[offset + 2] = 80; pixels[offset + 3] = 255; }
        var source = BitmapSource.Create(160, 100, 96, 96, PixelFormats.Bgra32, null, pixels, 160 * 4); source.Freeze(); return source;
    }
    [Fact]
    public void PngRoundTripAndThumbnailDecoding()
    {
        Sta(() =>
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
            try { File.WriteAllBytes(path, ImageService.Png(Synthetic())); var full = ImageService.Load(path); var thumb = ImageService.Load(path, 40); Assert.Equal(160, full.PixelWidth); Assert.Equal(40, thumb.PixelWidth); Assert.Equal(25, thumb.PixelHeight); }
            finally { File.Delete(path); }
            return true;
        });
    }
    [Fact]
    public void CropClipsAndDetachesFromOriginal()
    {
        Sta(() => { var crop = ImageService.Crop(Synthetic(), new(-10, -10, 50, 50)); Assert.Equal(40, crop.PixelWidth); Assert.Equal(40, crop.PixelHeight); Assert.False(crop is CroppedBitmap); Assert.Throws<InvalidDataException>(() => ImageService.Crop(Synthetic(), new(300, 300, 10, 10))); return true; });
    }
    [Fact]
    public void ExportedPngContainsAppliedOpaqueRedaction()
    {
        Sta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-export-pixels-" + Guid.NewGuid().ToString("N"));
            try
            {
                var repository = new CaptureRepository(Path.Combine(root, "cache")); var original = Synthetic();
                var record = repository.Add(ImageService.Png(original), original.PixelWidth, original.PixelHeight);
                var rendered = EditorWindow.Render(new(original, [new Annotation(EditTool.Redact, new(20, 20), new(100, 80), Colors.Black, 3, 24, "", [])]));
                repository.Replace(record, ImageService.Png(rendered), rendered.PixelWidth, rendered.PixelHeight);
                var path = Path.Combine(root, "output.png"); CaptureExport.Write(repository, record, path);
                var exported = ImageService.Load(path); var pixel = new byte[4]; exported.CopyPixels(new Int32Rect(50, 50, 1, 1), pixel, 4, 0);
                Assert.Equal(new byte[] { 0, 0, 0, 255 }, pixel);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            return true;
        });
    }
    [Theory]
    [InlineData(32, 3200)]
    [InlineData(3200, 32)]
    public void DockThumbnailBoundsBothDimensionsForExtremeAspectRatios(int width, int height)
    {
        Sta(() =>
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
            try
            {
                var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, new byte[width * height * 4], width * 4);
                File.WriteAllBytes(path, ImageService.Png(image));
                var thumbnail = ImageService.Load(path, 100, 80);
                Assert.InRange(thumbnail.PixelWidth, 1, 100); Assert.InRange(thumbnail.PixelHeight, 1, 80);
            }
            finally { File.Delete(path); }
            return true;
        });
    }
    [Theory]
    [InlineData(10)]
    [InlineData(9)]
    [InlineData(8)]
    public void EffectsRemoveOriginalPixelDetail(int toolValue)
    {
        Sta(() =>
        {
            var tool = (EditTool)toolValue; var source = Synthetic(); BitmapSource? patch = tool == EditTool.Redact ? null : Effects.Apply(ImageService.Crop(source, new(20, 20, 80, 60)), tool == EditTool.Pixelate);
            var mark = new Annotation(tool, new Point(20, 20), new Point(100, 80), Colors.Black, 3, 24, "", [], patch);
            var output = EditorWindow.Render(new(source, [mark])); var pixel = new byte[4]; output.CopyPixels(new Int32Rect(50, 50, 1, 1), pixel, 4, 0);
            Assert.Equal(255, pixel[3]);
            if (tool == EditTool.Redact) { Assert.Equal(0, pixel[0]); Assert.Equal(0, pixel[1]); Assert.Equal(0, pixel[2]); }
            else Assert.InRange(pixel[0], (byte)100, (byte)155);
            return true;
        });
    }
    [Fact]
    public void ClipboardImageAndPngFormatsAreConstructed()
    {
        Sta(() => { var image = Synthetic(); var data = new DataObject(); data.SetImage(image); data.SetData("PNG", new MemoryStream(ImageService.Png(image))); Assert.True(data.GetDataPresent(DataFormats.Bitmap)); Assert.True(data.GetDataPresent("PNG")); return true; });
    }
    [Fact]
    public void DragDataAndFileClipboardUseTheSameReorderedNonadjacentPayload()
    {
        Sta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-drag-order-" + Guid.NewGuid().ToString("N"));
            try
            {
                var repository = new CaptureRepository(root);
                for (var i = 0; i < 4; i++) repository.Add(ImageService.Png(Synthetic()), 160, 100);
                var captures = (List<CaptureRecord>)repository.Captures; var a = captures[0]; var c = captures[2];
                ShelfOrder.Move(captures, a.Id, c.Id);
                var request = new[] { a, c, a };
                var expected = TransferPayload.Files(repository, request);
                var drag = new DragDropService(repository).BuildData(request);
                Assert.Equal(expected, Assert.IsType<string[]>(drag.GetData(DataFormats.FileDrop)));
                Assert.False(drag.GetDataPresent(DataFormats.Bitmap));
                var single = new DragDropService(repository).BuildData([a, a]);
                Assert.True(single.GetDataPresent(DataFormats.Bitmap)); Assert.True(single.GetDataPresent("PNG"));
                Assert.Single(Assert.IsType<string[]>(single.GetData(DataFormats.FileDrop)));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            return true;
        });
    }
    [Fact]
    public async Task OcrRecognizesSyntheticErrorWithoutDesktop()
    {
        var bytes = Sta(() =>
        {
            var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) { dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 720, 360)); dc.DrawText(new FormattedText("Build failed\nError CS1002: semicolon expected", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), 32, Brushes.Black, 1), new Point(30, 70)); }
            var bitmap = new RenderTargetBitmap(720, 360, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); return ImageService.Png(bitmap);
        });
        var text = await new OcrService().ReadAsync(bytes); Assert.Contains("CS1002", text);
    }
    [Fact] public async Task OcrHonorsCancellation() { using var token = new CancellationTokenSource(); token.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new OcrService().ReadAsync([], token.Token)); }
    [Fact] public async Task MissingModelReturnsActionableError() { var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new OcrService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).ReadAsync([])); Assert.Contains("model is missing", error.Message); }
    [Fact]
    public void CorruptImportIsRejectedWithoutDesktop()
    {
        Sta(() => { var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png"); try { File.WriteAllText(path, "not an image"); Assert.ThrowsAny<Exception>(() => { ImageService.Load(path); }); } finally { File.Delete(path); } return true; });
    }
}
