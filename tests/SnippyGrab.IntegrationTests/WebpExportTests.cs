using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnippyGrab.App.Services;
using SnippyGrab.Core;

namespace SnippyGrab.IntegrationTests;

public sealed class WebpExportTests
{
    [Fact]
    public void CodecHeadersQualityAndCancellation()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var pixels = new byte[128 * 64 * 4]; new Random(42).NextBytes(pixels);
            var image = BitmapSource.Create(128, 64, 96, 96, PixelFormats.Bgra32, null, pixels, 128 * 4); image.Freeze();
            var lossless = WebpService.Encode(image, true, 90);
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(lossless, 0, 4));
            Assert.Equal("WEBPVP8L", System.Text.Encoding.ASCII.GetString(lossless, 8, 8));
            Assert.Equal((uint)lossless.Length - 8, BitConverter.ToUInt32(lossless, 4));
            Assert.True(WebpService.Encode(image, false, 20).Length < WebpService.Encode(image, false, 100).Length);
            Assert.Throws<OperationCanceledException>(() => WebpService.Encode(image, true, 90, new CancellationToken(true)));
            Assert.Throws<InvalidDataException>(() => WebpService.Encode(image, false, 101));
            return true;
        });
    }
    [Fact]
    public void LimitsAndMissingOrChangedCodecFailClosed()
    {
        WebpService.ValidateDimensions(16383, 1); WebpService.ValidateDimensions(4000, 4000);
        Assert.Throws<InvalidDataException>(() => WebpService.ValidateDimensions(16384, 1));
        Assert.Throws<InvalidDataException>(() => WebpService.ValidateDimensions(4001, 4000));
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-webp-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<InvalidDataException>(() => WebpService.ValidatePackage(root));
            Directory.CreateDirectory(Path.Combine(root, "x64"));
            File.Copy(Path.Combine(AppContext.BaseDirectory, "WEBP-PROVENANCE.json"), Path.Combine(root, "WEBP-PROVENANCE.json"));
            File.WriteAllBytes(Path.Combine(root, "x64", "snippywebp.dll"), [1, 2, 3]);
            Assert.Throws<InvalidDataException>(() => WebpService.ValidatePackage(root));
            File.WriteAllText(Path.Combine(root, "WEBP-PROVENANCE.json"), "{}");
            Assert.Throws<InvalidDataException>(() => WebpService.ValidatePackage(root));
            WebpService.ValidatePackage(AppContext.BaseDirectory);
        }
        finally { Directory.Delete(root, true); }
    }
    [Theory]
    [InlineData(ExportFormat.WebpLossless)]
    [InlineData(ExportFormat.WebpLossy)]
    public async Task WebpUsesSharedExportAndBatchBoundary(ExportFormat format)
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-webp-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            var png = ImageIntegrationTests.Sta(() => ImageService.Png(BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, new byte[64], 16)));
            var repository = new CaptureRepository(Path.Combine(root, "cache")); var capture = repository.Add(png, 4, 4);
            var destination = Path.Combine(root, "image.webp");
            Assert.Throws<InvalidDataException>(() => CaptureExport.ValidateDestination(repository.Root, Path.Combine(root, "image.png"), format));
            await CaptureExport.WriteAsync(repository, capture, destination, format, 90, (path, quality) => ImageIntegrationTests.Sta(() => ImageService.EncodeExport(path, format, quality, default)));
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(destination), 0, 4)); Assert.Equal(png, File.ReadAllBytes(repository.PathFor(capture)));
            var plan = BatchExport.Plan(repository, [capture], root, format, ExportCollision.Unique);
            Assert.EndsWith(".webp", plan[0].Destination);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
