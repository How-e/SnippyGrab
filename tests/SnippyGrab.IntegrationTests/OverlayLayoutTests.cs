using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnippyGrab.App.Services;
using SnippyGrab.App.Views;
using SnippyGrab.Core;

namespace SnippyGrab.IntegrationTests;

public sealed class OverlayLayoutTests
{
    [Theory]
    [InlineData(6400, 1821)]
    [InlineData(3840, 4320)]
    [InlineData(1920, 1080)]
    public void ShownOverlayKeepsFullPhysicalBoundsAndMatchingContent(int width, int height)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            CaptureOverlay? overlay = null;
            try
            {
                var image = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8); image.Freeze();
                // Entirely offscreen: no pointer movement, screen capture, clipboard write or focus change.
                var expected = new PixelRect(-30000, -30000, width, height);
                var foreground = Native.GetForegroundWindow();
                overlay = new CaptureOverlay(image, expected, false, activate: false) { Topmost = false };
                overlay.Show(); overlay.UpdateLayout();
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => frame.Continue = false);
                Dispatcher.PushFrame(frame); overlay.UpdateLayout();
                Native.GetWindowRect(new WindowInteropHelper(overlay).Handle, out var actual);
                Assert.Equal(expected, actual.Pixels);
                var dpi = VisualTreeHelper.GetDpi(overlay);
                Assert.Equal(width, overlay.ActualWidth * dpi.DpiScaleX, 0);
                Assert.Equal(height, overlay.ActualHeight * dpi.DpiScaleY, 0);
                var view = Assert.IsType<System.Windows.Controls.Viewbox>(overlay.Content);
                var rendered = view.Child.TransformToAncestor(overlay).TransformBounds(new Rect(0, 0, width, height));
                Assert.Equal(0, rendered.X, 0); Assert.Equal(0, rendered.Y, 0);
                Assert.Equal(width, rendered.Width * dpi.DpiScaleX, 0);
                Assert.Equal(height, rendered.Height * dpi.DpiScaleY, 0);
                // Later WPF/native layout requests must not undo the initial full-desktop geometry.
                Native.SetWindowPos(new WindowInteropHelper(overlay).Handle, 0, -29000, -29000, 800, 600, 0x0014);
                overlay.UpdateLayout(); Native.GetWindowRect(new WindowInteropHelper(overlay).Handle, out actual);
                Assert.Equal(expected, actual.Pixels);
                Assert.Equal(foreground, Native.GetForegroundWindow());
            }
            catch (Exception ex) { error = ex; }
            finally { overlay?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
