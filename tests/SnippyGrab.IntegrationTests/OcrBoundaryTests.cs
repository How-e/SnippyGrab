using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnippyGrab.App.Services;
using SnippyGrab.Core;

namespace SnippyGrab.IntegrationTests;

public sealed class OcrBoundaryTests
{
    private static byte[] TextImage(string text, bool area)
    {
        return ImageIntegrationTests.Sta(() =>
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 1200, 500));
                dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), 30, Brushes.Black, 1), new Point(40, 60));
            }
            var bitmap = new RenderTargetBitmap(1200, 500, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze();
            return ImageService.Png(area ? ImageService.Crop(bitmap, new PixelRect(20, 40, 1160, 250)) : bitmap);
        });
    }
    [Theory]
    // The model can confuse 0/@ in this 30px fixture. Exact 32px CS1002 coverage stays in ImageIntegrationTests.
    [InlineData("ERROR CS1002: semicolon expected\nBuild failed", "semicolon", false)]
    [InlineData("Traceback (most recent call last):\nValueError: invalid literal", "ValueError", true)]
    [InlineData("Application error\nAccess denied. Please retry.", "denied", false)]
    public async Task LocalFullAndAreaOcrRecognizesRepresentativeText(string text, string expected, bool area)
    {
        Assert.Contains(expected, await new OcrService().ReadAsync(TextImage(text, area)), StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task QueuedCallsCancelAndMalformedInputDoesNotPoisonNextRequest()
    {
        var service = new OcrService(); var image = TextImage("ERROR CS1002: semicolon expected", false);
        var first = service.ReadAsync(image);
        using var cancellation = new CancellationTokenSource(); var queued = service.ReadAsync(image, cancellation.Token); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.Contains("expected", await first);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ReadAsync(new byte[30]));
        var repeats = await Task.WhenAll(service.ReadAsync(image), service.ReadAsync(image));
        Assert.All(repeats, text => Assert.Contains("expected", text));
    }
    [Fact]
    public void WrappedNativeLoaderFailuresAreRecognized()
    {
        Assert.True(OcrService.HasNativeLoaderFailure(new TypeInitializationException("fixture", new DllNotFoundException())));
        Assert.True(OcrService.HasNativeLoaderFailure(new Exception("fixture", new BadImageFormatException())));
        Assert.False(OcrService.HasNativeLoaderFailure(new InvalidDataException()));
    }
    [Fact]
    public async Task ModifiedModelIsRejectedBeforeNativeParsing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SnippyGrab-model-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "eng.traineddata"), "untrusted fixture");
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new OcrService(directory).ReadAsync(new byte[30]));
            Assert.Contains("checksum failed", error.Message);
        }
        finally { Directory.Delete(directory, true); }
    }
}
