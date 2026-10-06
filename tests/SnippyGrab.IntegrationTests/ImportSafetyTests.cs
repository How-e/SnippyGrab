using System.Buffers.Binary;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnippyGrab.App.Services;

namespace SnippyGrab.IntegrationTests;

public sealed class ImportSafetyTests
{
    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("bmp")]
    public void SupportedCodecsAreDecodedFromContent(string codec)
    {
        ImageIntegrationTests.Sta(() =>
        {
            var image = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgr32, null, new byte[64], 16);
            BitmapEncoder encoder = codec switch { "png" => new PngBitmapEncoder(), "jpeg" => new JpegBitmapEncoder(), _ => new BmpBitmapEncoder() };
            encoder.Frames.Add(BitmapFrame.Create(image));
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
            try { using (var stream = File.Create(path)) encoder.Save(stream); Assert.Equal(4, ImageService.Load(path).PixelWidth); }
            finally { File.Delete(path); }
            return true;
        });
    }
    [Fact]
    public void GifDisguisedAsPngAndTruncatedPngAreRejected()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
            try
            {
                File.WriteAllBytes(path, "GIF89a"u8.ToArray().Concat(new byte[100]).ToArray());
                Assert.Throws<InvalidDataException>(() => ImageService.Load(path));
                var image = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgr32, null, new byte[64], 16);
                var png = ImageService.Png(image); File.WriteAllBytes(path, png[..^12]);
                Assert.Throws<InvalidDataException>(() => ImageService.Load(path));
                BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16, 4), 100000);
                BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20, 4), 100000);
                File.WriteAllBytes(path, png); Assert.Throws<InvalidDataException>(() => ImageService.Load(path));
                using (var file = File.Create(path)) file.SetLength(100L * 1024 * 1024 + 1);
                Assert.Throws<InvalidDataException>(() => ImageService.Load(path));
            }
            finally { File.Delete(path); }
            return true;
        });
    }
}
