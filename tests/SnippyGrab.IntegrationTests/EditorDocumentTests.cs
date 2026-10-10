using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnippyGrab.App.Services;
using SnippyGrab.App.Views;
using SnippyGrab.Core;

namespace SnippyGrab.IntegrationTests;

public sealed class EditorDocumentTests
{
    [Fact]
    public void ColorPickerSamplesFlattenedEditsAndClampsEdges()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var source = Fixture();
            var edited = EditorWindow.Render(new(source, [Mark(EditTool.Redact)]));
            Assert.Equal(Colors.Black, EditorWindow.SampleColor(edited, new(50, 40)));
            Assert.Equal(Colors.RoyalBlue, EditorWindow.SampleColor(source, new(-5, -5)));
            Assert.Equal(Colors.White, EditorWindow.SampleColor(source, new(160, 100)));
            Assert.Throws<ArgumentOutOfRangeException>(() => EditorWindow.SampleColor(source, new(double.NaN, 0)));
            Assert.NotEqual(Colors.Black, EditorWindow.SampleColor(source, new(50, 40)));
            return true;
        });
    }
    internal static BitmapSource Fixture(int width = 160, int height = 100)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            for (var x = 0; x < width; x += 8) dc.DrawRectangle(Brushes.RoyalBlue, null, new Rect(x, 0, 4, height));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    private static Annotation Mark(EditTool tool) => new(tool, new Point(110, 70), new Point(30, 20), Colors.Red, 4, 20, "TEST", [new(20, 20), new(90, 40), new(110, 60)]);
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(12)]
    public void RequiredToolsFlattenAndUndoRedoWithoutChangingSource(int toolNumber)
    {
        ImageIntegrationTests.Sta(() =>
        {
            var initial = new EditorState(Fixture(), []); var before = ImageService.Png(initial.Base);
            var journal = new UndoJournal<EditorState>(initial, 20, EditorWindow.RetainedBytes, 256 * 1024 * 1024);
            var tool = (EditTool)toolNumber;
            journal.Push(EditorWindow.BuildStateAsync(journal.Current, Mark(tool)).GetAwaiter().GetResult());
            var after = ImageService.Png(EditorWindow.Render(journal.Current));
            Assert.False(before.SequenceEqual(after)); Assert.True(EditorWindow.Render(journal.Current).IsFrozen);
            Assert.Equal(before, ImageService.Png(journal.Undo().Base));
            Assert.Equal(after, ImageService.Png(EditorWindow.Render(journal.Redo())));
            journal.Undo(); journal.Push(EditorWindow.BuildStateAsync(journal.Current, Mark(EditTool.Line)).GetAwaiter().GetResult());
            Assert.False(journal.CanRedo); Assert.Equal(before, ImageService.Png(initial.Base));
            if (tool == EditTool.Redact)
            {
                var pixel = new byte[4]; EditorWindow.Render(new(initial.Base, [Mark(tool)])).CopyPixels(new Int32Rect(50, 40, 1, 1), pixel, 4, 0);
                Assert.Equal(new byte[] { 0, 0, 0, 255 }, pixel);
            }
            return true;
        });
    }
    [Fact]
    public void CropResetsCoordinatesAndPriorTransferredRevisionRemainsUnchanged()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-editor-document-" + Guid.NewGuid().ToString("N"));
            try
            {
                var repository = new CaptureRepository(root); var initial = new EditorState(Fixture(), []);
                var original = ImageService.Png(initial.Base); var record = repository.Add(original, 160, 100);
                using var transfer = repository.Lease([record], transfer: true); var oldPath = repository.PathFor(record);
                var cropped = EditorWindow.BuildStateAsync(initial, Mark(EditTool.Crop)).GetAwaiter().GetResult();
                Assert.Equal(80, cropped.Base.PixelWidth); Assert.Equal(50, cropped.Base.PixelHeight); Assert.Empty(cropped.Marks);
                var final = EditorWindow.BuildStateAsync(cropped, new(EditTool.Redact, new(5, 5), new(20, 20), Colors.Red, 4, 20, "", [])).GetAwaiter().GetResult();
                var image = EditorWindow.Render(final); repository.Replace(record, ImageService.Png(image), image.PixelWidth, image.PixelHeight);
                Assert.Equal(original, File.ReadAllBytes(oldPath)); Assert.NotEqual(oldPath, repository.PathFor(record));
                var pixel = new byte[4]; image.CopyPixels(new Int32Rect(10, 10, 1, 1), pixel, 4, 0); Assert.Equal(255, pixel[3]); Assert.Equal(0, pixel[0]);
            }
            finally { Directory.Delete(root, true); }
            return true;
        });
    }
    [Fact]
    public void RetainedMemoryCountsSharedImagesOnceAndLabelsAreReadable()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var image = Fixture(); var mark = Mark(EditTool.Pen); var state = new EditorState(image, [mark]);
            Assert.Equal(EditorWindow.RetainedBytes([state]), EditorWindow.RetainedBytes([state, state with { Marks = [mark] }]));
            Assert.Equal("OCR selected area", EditorWindow.ToolLabel(EditTool.OcrArea));
            return true;
        });
    }
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(12)]
    public void RegionRenderingMatchesFullRenderingIncludingExistingEffects(int tool)
    {
        ImageIntegrationTests.Sta(() =>
        {
            var state = new EditorState(Fixture(), []);
            state = EditorWindow.BuildStateAsync(state, Mark((EditTool)tool)).GetAwaiter().GetResult();
            var area = new PixelRect(25, 18, 80, 50);
            var full = new FormatConvertedBitmap(ImageService.Crop(EditorWindow.Render(state), area), PixelFormats.Bgra32, null, 0);
            var region = new FormatConvertedBitmap(EditorWindow.RenderRegion(state, area), PixelFormats.Bgra32, null, 0);
            var before = new byte[area.Width * area.Height * 4]; var after = new byte[before.Length];
            full.CopyPixels(before, area.Width * 4, 0); region.CopyPixels(after, area.Width * 4, 0);
            Assert.Equal(before, after); return true;
        });
    }
    [Fact]
    public void RepeatedCropsShareBackingPixelsAndRetainUndoWithinBudget()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var initial = new EditorState(Fixture(), []);
            var journal = new UndoJournal<EditorState>(initial, 20, EditorWindow.RetainedBytes, 160 * 100 * 4);
            for (var i = 0; i < 3; i++) journal.Push(EditorWindow.BuildStateAsync(journal.Current, new(EditTool.Crop, new(2, 2), new(journal.Current.Base.PixelWidth - 2, journal.Current.Base.PixelHeight - 2), Colors.Red, 3, 24, "", [])).GetAwaiter().GetResult());
            Assert.Equal(EditorWindow.RetainedBytes([initial]), EditorWindow.RetainedBytes([initial, journal.Current]));
            var steps = 0; while (journal.CanUndo) { journal.Undo(); steps++; }
            Assert.Equal(3, steps); Assert.Same(initial.Base, journal.Current.Base); return true;
        });
    }
}
