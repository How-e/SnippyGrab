using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnippyGrab.App.Services;
using SnippyGrab.Core;

namespace SnippyGrab.IntegrationTests;

public sealed class JpegExportTests
{
    [Fact]
    public void ScanlineExportPreservesRowOrderPaddingResolutionAndCancellation()
    {
        ImageIntegrationTests.Sta(() =>
        {
            const int width = 7, height = 48; var pixels = new byte[width * height * 4];
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4;
                if (y < 16) pixels[offset + 2] = 255;
                else if (y < 32) { pixels[offset + 1] = 255; pixels[offset + 3] = 128; }
                else { pixels[offset] = 255; pixels[offset + 3] = 255; }
            }
            var source = BitmapSource.Create(width, height, 144, 144, PixelFormats.Bgra32, null, pixels, width * 4); source.Freeze();
            Assert.ThrowsAny<OperationCanceledException>(() => ImageService.Jpeg(source, 90, new CancellationToken(true)));
            using var stream = new MemoryStream(ImageService.Jpeg(source, 100));
            var decoded = new JpegBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Assert.Equal(width, decoded.PixelWidth); Assert.Equal(height, decoded.PixelHeight); Assert.InRange(decoded.DpiX, 95.9, 96.1);
            var converted = new FormatConvertedBitmap(decoded, PixelFormats.Bgr32, null, 0); var pixel = new byte[4];
            foreach (var y in new[] { 8, 24, 40 })
            {
                converted.CopyPixels(new System.Windows.Int32Rect(3, y, 1, 1), pixel, 4, 0);
                var expected = y == 8 ? new[] { 255, 255, 255 } : y == 24 ? new[] { 127, 255, 127 } : new[] { 255, 0, 0 };
                for (var c = 0; c < 3; c++) Assert.InRange((int)pixel[c], Math.Max(0, expected[c] - 6), Math.Min(255, expected[c] + 6));
            }
            return true;
        });
    }
    [Fact]
    public void JpegPreservesDimensionsFlattensAlphaAndControlsQuality()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var pixels = new byte[96 * 48 * 4];
            for (var y = 0; y < 48; y++) for (var x = 0; x < 96; x++)
            {
                var i = (y * 96 + x) * 4;
                if (x < 32) { pixels[i] = 255; pixels[i + 3] = 0; }
                else if (x < 64) pixels[i + 3] = 128;
                else { pixels[i + 2] = 255; pixels[i + 3] = 255; }
            }
            var image = BitmapSource.Create(96, 48, 144, 144, PixelFormats.Bgra32, null, pixels, 96 * 4); image.Freeze();
            var bytes = ImageService.Jpeg(image, 90); Assert.Equal(new byte[] { 255, 216 }, bytes[..2]);
            using var stream = new MemoryStream(bytes); var decoded = new JpegBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Assert.Equal(96, decoded.PixelWidth); Assert.Equal(48, decoded.PixelHeight);
            var converted = new FormatConvertedBitmap(decoded, PixelFormats.Bgr32, null, 0); var after = new byte[pixels.Length]; converted.CopyPixels(after, 96 * 4, 0);
            foreach (var x in new[] { 8, 48, 80 })
            {
                var i = (24 * 96 + x) * 4; var expected = x == 8 ? new[] { 255, 255, 255 } : x == 48 ? new[] { 127, 127, 127 } : new[] { 0, 0, 255 };
                for (var c = 0; c < 3; c++) Assert.InRange((int)after[i + c], Math.Max(0, expected[c] - 6), Math.Min(255, expected[c] + 6));
            }
            var random = new Random(42); random.NextBytes(pixels); for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            var noise = BitmapSource.Create(96, 48, 96, 96, PixelFormats.Bgra32, null, pixels, 96 * 4); noise.Freeze();
            Assert.True(ImageService.Jpeg(noise, 30).Length < ImageService.Jpeg(noise, 100).Length);
            Assert.Throws<InvalidDataException>(() => ImageService.Jpeg(noise, 101)); return true;
        });
    }
    [Fact]
    public async Task ExportPngPreservesAncillaryMetadataWithoutEncoding()
    {
        var bytes = ImageIntegrationTests.Sta(() =>
        {
            var image = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8);
            var metadata = new BitmapMetadata("png"); metadata.SetQuery("/tEXt/{str=Description}", "Synthetic export fixture");
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image, null, metadata, null));
            using var encoded = new MemoryStream(); encoder.Save(encoded); return encoded.ToArray();
        });
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-export-pixels-" + Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new CaptureRepository(Path.Combine(root, "cache")); var capture = repository.Add(bytes, 2, 2);
            var path = Path.Combine(root, "image.png"); await CaptureExport.WriteAsync(repository, capture, path, ExportFormat.Png, 90, (_, _) => throw new Exception());
            Assert.Equal(bytes, File.ReadAllBytes(path)); Assert.Equal(bytes, File.ReadAllBytes(repository.PathFor(capture)));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
