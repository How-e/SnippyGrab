using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnippyGrab.App.Services;
using SnippyGrab.Core;

namespace SnippyGrab.IntegrationTests;

public sealed class CompositionPixelTests
{
    [Fact]
    public void CompositeSeamsPaddingAndAlphaAreCorrectAndOriginalsUnchanged()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-composite-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var paths = new[] { Path.Combine(root, "one.png"), Path.Combine(root, "two.png") };
                var one = ImageService.Png(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 255 }, 4));
                var two = ImageService.Png(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 128 }, 4));
                File.WriteAllBytes(paths[0], one); File.WriteAllBytes(paths[1], two);
                var layout = Composition.Plan([(1, 1), (1, 1)], new(CompositionMode.Vertical, 1));
                var image = CompositionService.Render(paths, layout, Colors.White); var pixels = new byte[12]; image.CopyPixels(pixels, 4, 0);
                Assert.Equal(new byte[] { 0, 0, 255, 255, 255, 255, 255, 255, 127, 127, 127, 255 }, pixels);
                Assert.Equal(one, File.ReadAllBytes(paths[0])); Assert.Equal(two, File.ReadAllBytes(paths[1]));
                using var cancel = new CancellationTokenSource(); cancel.Cancel(); Assert.ThrowsAny<OperationCanceledException>(() => CompositionService.Render(paths, layout, Colors.Transparent, cancel.Token));
                Assert.Equal(3, ImageService.Load(Write()).PixelHeight); return true;
                string Write() { var p = Path.Combine(root, "combined.png"); File.WriteAllBytes(p, ImageService.Png(image)); return p; }
            }
            finally { Directory.Delete(root, true); }
        });
    }
}
