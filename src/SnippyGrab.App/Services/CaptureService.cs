using System.Diagnostics;
using System.Runtime.InteropServices;
using SnippyGrab.App.Views;

namespace SnippyGrab.App.Services;

internal sealed record CaptureResult(BitmapSource Image, PixelRect Bounds, long ElapsedMilliseconds);
internal sealed class CaptureService
{
    public bool Busy { get; private set; }
    public event Action<string>? Timing;
    public async Task<CaptureResult?> CaptureAsync(CaptureMode mode, bool cursor, Action hide, Action restore)
    {
        if (Busy) return null;
        Busy = true;
        var previous = Native.GetForegroundWindow();
        var stopwatch = Stopwatch.StartNew();
        BitmapSource? frozen = null;
        try
        {
            hide(); await Task.Delay(45); Native.DwmFlush();
            var desktop = Native.Desktop;
            var bounds = mode == CaptureMode.ActiveWindow ? WindowBounds(previous).Intersect(desktop) : desktop;
            if (mode is CaptureMode.Desktop or CaptureMode.ActiveWindow)
                return new(ImageService.Capture(bounds, cursor), bounds, stopwatch.ElapsedMilliseconds);
            frozen = ImageService.Capture(desktop, cursor);
            var overlay = new CaptureOverlay(frozen, desktop, mode == CaptureMode.Window);
            var completion = new TaskCompletionSource();
            overlay.Closed += (_, _) => completion.TrySetResult();
            overlay.Show(); Timing?.Invoke($"request_to_overlay_ms={stopwatch.ElapsedMilliseconds}");
            await completion.Task;
            var selectionWork = Stopwatch.StartNew();
            if (overlay.WindowPoint is { } point)
            {
                Native.DwmFlush();
                var target = Native.GetAncestor(Native.WindowFromPoint(point), 2);
                bounds = WindowBounds(target).Intersect(desktop);
                if (bounds.IsEmpty) return null;
                // Freeze-to-select also prevents hover UI from becoming part of the selected window.
                return new(ImageService.Crop(frozen, bounds.RelativeTo(desktop)), bounds, stopwatch.ElapsedMilliseconds);
            }
            if (overlay.Selection is not { } selection) return null;
            var cropped = ImageService.Crop(frozen, selection.RelativeTo(desktop));
            Timing?.Invoke($"selection_to_crop_ms={selectionWork.ElapsedMilliseconds}");
            return new(cropped, selection, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            frozen = null; Busy = false;
            if (previous != 0) Native.SetForegroundWindow(previous);
            restore();
        }
    }
    private static PixelRect WindowBounds(nint window)
    {
        if (window == 0) throw new InvalidOperationException("No window is available to capture.");
        if (Native.DwmGetWindowAttribute(window, 9, out var rect, Marshal.SizeOf<Native.RECT>()) != 0)
            Native.GetWindowRect(window, out rect);
        return rect.Pixels;
    }
}
