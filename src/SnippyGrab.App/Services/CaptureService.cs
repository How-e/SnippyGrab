using System.Diagnostics;
using System.Runtime.InteropServices;
using SnippyGrab.App.Views;

namespace SnippyGrab.App.Services;

internal sealed record CaptureResult(BitmapSource Image, PixelRect Bounds, long ElapsedMilliseconds, nint TargetWindow = 0);
internal sealed class CaptureService
{
    public bool Busy { get; private set; }
    public event Action<string>? Timing;
    public async Task<CaptureResult?> CaptureAsync(CaptureMode mode, bool cursor, Action hide, Action restore, CaptureTarget? target = null, CaptureTiming? timing = null)
    {
        if (Busy) return null;
        Busy = true;
        var previous = Native.GetForegroundWindow();
        var stopwatch = Stopwatch.StartNew();
        BitmapSource? frozen = null;
        try
        {
            using (timing?.Measure(CapturePhase.Hide)) { hide(); await Task.Delay(45); Native.DwmFlush(); }
            var desktop = Native.Desktop;
            if (mode == CaptureMode.Monitor)
            {
                if (target is null) throw new InvalidOperationException("Choose a display before capturing.");
                var monitorBounds = MonitorCapture.Resolve(MonitorService.All().Select(m => (m.Identity, m.Bounds)), target);
                return new(await AcquireAsync(() => ImageService.Capture(monitorBounds, cursor), timing), monitorBounds, stopwatch.ElapsedMilliseconds);
            }
            var bounds = mode == CaptureMode.ActiveWindow ? WindowBounds(previous).Intersect(desktop) : desktop;
            if (mode is CaptureMode.Desktop or CaptureMode.ActiveWindow)
                return new(await AcquireAsync(() => ImageService.Capture(bounds, cursor), timing), bounds, stopwatch.ElapsedMilliseconds);
            frozen = await AcquireAsync(() => ImageService.Capture(desktop, cursor), timing);
            var overlay = new CaptureOverlay(frozen, desktop, mode == CaptureMode.Window);
            if (timing is not null) overlay.SelectionAccepted += timing.SelectionCompleted;
            var completion = new TaskCompletionSource();
            overlay.Closed += (_, _) => completion.TrySetResult();
            overlay.Show(); timing?.OverlayShown(); Timing?.Invoke($"request_to_overlay_ms={stopwatch.ElapsedMilliseconds}");
            await completion.Task;
            var selectionWork = Stopwatch.StartNew();
            if (overlay.WindowPoint is { } point)
            {
                Native.DwmFlush();
                var windowTarget = Native.GetAncestor(Native.WindowFromPoint(point), 2);
                bounds = WindowBounds(windowTarget).Intersect(desktop);
                if (bounds.IsEmpty) return null;
                // Freeze-to-select also prevents hover UI from becoming part of the selected window.
                using var cropPhase = timing?.Measure(CapturePhase.Crop);
                return new(await ImageWorker.RunCaptureAsync(() => ImageService.Crop(frozen, bounds.RelativeTo(desktop))), bounds, stopwatch.ElapsedMilliseconds);
            }
            if (overlay.Selection is not { } selection) return null;
            BitmapSource cropped;
            using (timing?.Measure(CapturePhase.Crop)) cropped = await ImageWorker.RunCaptureAsync(() => ImageService.Crop(frozen, selection.RelativeTo(desktop)));
            Timing?.Invoke($"selection_to_crop_ms={selectionWork.ElapsedMilliseconds}");
            // Resolve while shelf/pins are still hidden, before restore can cover the viewport.
            var selectedWindow = Native.GetAncestor(Native.WindowFromPoint(new Native.POINT { X = selection.X + selection.Width / 2, Y = selection.Y + selection.Height / 2 }), 2);
            return new(cropped, selection, stopwatch.ElapsedMilliseconds, selectedWindow);
        }
        finally
        {
            frozen = null; Busy = false;
            if (previous != 0) Native.SetForegroundWindow(previous);
            restore();
        }
    }
    private static async Task<BitmapSource> AcquireAsync(Func<BitmapSource> capture, CaptureTiming? timing)
    {
        using var phase = timing?.Measure(CapturePhase.Acquisition);
        return await ImageWorker.RunCaptureAsync(capture);
    }
    private static PixelRect WindowBounds(nint window)
    {
        if (window == 0) throw new InvalidOperationException("No window is available to capture.");
        if (Native.DwmGetWindowAttribute(window, 9, out var rect, Marshal.SizeOf<Native.RECT>()) != 0)
            Native.GetWindowRect(window, out rect);
        return rect.Pixels;
    }
}
