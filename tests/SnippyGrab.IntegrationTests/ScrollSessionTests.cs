using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnippyGrab.App.Services;

namespace SnippyGrab.IntegrationTests;

public sealed class ScrollSessionTests
{
    [Fact]
    public void FrameCapViewportCapAndRepeatedDisposalAreBounded()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-scroll-limits-" + Guid.NewGuid().ToString("N"));
            try
            {
                using var session = new ScrollSession(root);
                var frame = BitmapSource.Create(1, 32, 96, 96, PixelFormats.Bgra32, null, new byte[128], 4); frame.Freeze();
                session.Stage(frame);
                for (var i = 1; i < 30; i++) { session.Stage(frame); session.Accept(0, 32); }
                Assert.Equal(30, session.Frames.Count); Assert.Throws<InvalidOperationException>(() => session.Stage(frame));
                session.Undo(); session.Stage(frame); session.Reject(); Assert.Equal(29, session.Frames.Count);
                var wide = BitmapSource.Create(4097, 32, 96, 96, PixelFormats.Bgra32, null, new byte[4097 * 32 * 4], 4097 * 4); wide.Freeze();
                Assert.Throws<InvalidDataException>(() => session.Stage(wide));
                session.Dispose(); session.Dispose(); Assert.Empty(Directory.GetDirectories(root)); return true;
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        });
    }
    [Fact]
    public void SessionReconstructsEveryRowOnceAndUndoRejectCancelLeaveNoArtifacts()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-scroll-fixture-" + Guid.NewGuid().ToString("N"));
            try
            {
                const int width = 40, height = 192; var pixels = new byte[width * height * 4]; var random = new Random(321); random.NextBytes(pixels); for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
                BitmapSource Frame(int offset) { var image = BitmapSource.Create(width, 128, 96, 96, PixelFormats.Bgra32, null, pixels[(width * offset * 4)..(width * (offset + 128) * 4)], width * 4); image.Freeze(); return image; }
                using (var session = new ScrollSession(root))
                {
                    session.Stage(Frame(0)); session.Stage(Frame(64)); Assert.NotNull(session.Pending);
                    Assert.Throws<InvalidOperationException>(() => session.Build(0, 0, default)); session.Accept(64, 128);
                    var result = session.Build(0, 0, default); Assert.Equal(height, result.PixelHeight); var after = new byte[pixels.Length]; result.CopyPixels(after, width * 4, 0); Assert.Equal(pixels, after);
                    session.Undo(); Assert.Single(session.Frames); session.Stage(Frame(64)); session.Reject(); Assert.Null(session.Pending);
                    using var cancel = new CancellationTokenSource(); cancel.Cancel(); Assert.ThrowsAny<OperationCanceledException>(() => session.Build(0, 0, cancel.Token));
                    Assert.Equal(118, session.Build(5, 5, default).PixelHeight);
                    Assert.Throws<InvalidDataException>(() => session.Build(int.MaxValue, int.MaxValue, default));
                    Assert.Throws<InvalidDataException>(() => session.Build(-1, 0, default));
                }
                Assert.Empty(Directory.GetDirectories(root)); return true;
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        });
    }
    [Fact]
    public void StaleCleanupRequiresOwnershipAndKnownInventory()
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-scroll-sweep-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var owned = Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(owned); File.WriteAllText(Path.Combine(owned, "owner.txt"), "SnippyGrab-scroll-v1"); Directory.SetCreationTimeUtc(owned, DateTime.UtcNow.AddDays(-2));
            File.WriteAllBytes(Path.Combine(owned, "frame-" + Guid.NewGuid().ToString("N") + ".png." + Guid.NewGuid().ToString("N") + ".tmp"), [1, 2, 3]);
            var guarded = Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(guarded); File.WriteAllText(Path.Combine(guarded, "owner.txt"), "SnippyGrab-scroll-v1"); Directory.SetCreationTimeUtc(guarded, DateTime.UtcNow.AddDays(-2));
            File.WriteAllText(Path.Combine(guarded, "frame-" + Guid.NewGuid().ToString("N") + ".png.unknown.tmp"), "fixture");
            var unknown = Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(unknown); File.WriteAllText(Path.Combine(unknown, "private.txt"), "fixture"); Directory.SetCreationTimeUtc(unknown, DateTime.UtcNow.AddDays(-2));
            ScrollSession.Sweep(root); Assert.False(Directory.Exists(owned)); Assert.True(Directory.Exists(unknown));
            Assert.True(Directory.Exists(guarded));
        }
        finally { Directory.Delete(root, true); }
    }
}
