using System.Windows.Interop;
using System.Windows.Threading;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

// Keep native chrome: Windows owns maximize, snap, restore and taskbar insets.
internal static class WindowPlacement
{
    internal static void Attach(Window window, MonitorInfo? initialMonitor = null)
    {
        var anchor = initialMonitor ?? Anchor();
        var minimumWidth = 0d; var minimumHeight = 0d;
        var explicitPosition = false;
        window.SourceInitialized += (_, _) =>
        {
            explicitPosition = window.WindowStartupLocation == WindowStartupLocation.Manual && double.IsFinite(window.Left) && double.IsFinite(window.Top);
            minimumWidth = window.MinWidth; minimumHeight = window.MinHeight;
            HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)?.AddHook(Hook);
        };
        window.Loaded += (_, _) =>
        {
            // Explicit coordinates are used by pins/diagnostics and caller placement.
            if (explicitPosition) return;
            var owner = window.Owner;
            var monitor = owner is { IsVisible: true } ? ForWindow(owner) : Current(anchor);
            Fit(monitor, center: true);
        };
        nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
        {
            if (message is 0x02E0 or 0x007E or 0x001A) // DPI, display topology, work area
                window.Dispatcher.BeginInvoke(() =>
                {
                    if (!window.IsVisible) return;
                    var monitor = ForWindow(window);
                    LimitMinimum(monitor);
                    if (window.WindowState == WindowState.Normal) Fit(monitor, center: false);
                }, DispatcherPriority.Loaded);
            return 0;
        }
        void LimitMinimum(MonitorInfo monitor)
        {
            var scale = Math.Max(96, monitor.Dpi) / 96d;
            window.MinWidth = Math.Min(minimumWidth, Math.Max(1, monitor.Work.Width - 24) / scale);
            window.MinHeight = Math.Min(minimumHeight, Math.Max(1, monitor.Work.Height - 24) / scale);
        }
        void Fit(MonitorInfo monitor, bool center)
        {
            if (window.WindowState != WindowState.Normal || monitor.Work.IsEmpty) return;
            LimitMinimum(monitor);
            var hwnd = new WindowInteropHelper(window).Handle;
            if (!Native.GetWindowRect(hwnd, out var rect)) return;
            var scale = Math.Max(96, monitor.Dpi) / 96d;
            var width = center && double.IsFinite(window.Width) ? (int)Math.Ceiling(window.Width * scale) : rect.Pixels.Width;
            var height = center && double.IsFinite(window.Height) ? (int)Math.Ceiling(window.Height * scale) : rect.Pixels.Height;
            var bounds = Constrain(monitor.Work, new(rect.Left, rect.Top, width, height), center);
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            Native.SetWindowPos(hwnd, 0, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0014); // no activate/z-order
        }
    }
    internal static MonitorInfo Anchor()
    {
        var foreground = Native.GetForegroundWindow();
        var active = Application.Current?.Windows.Cast<Window>().FirstOrDefault(w => w.IsVisible && new WindowInteropHelper(w).Handle == foreground);
        return active is null ? MonitorService.ForPointer() : ForWindow(active);
    }
    private static MonitorInfo Current(MonitorInfo preferred)
    {
        var monitors = MonitorService.All();
        return monitors.FirstOrDefault(m => m.Identity == preferred.Identity) ?? MonitorService.ForPointer();
    }
    internal static MonitorInfo ForWindow(Window window)
    {
        var handle = Native.MonitorFromWindow(new WindowInteropHelper(window).Handle, 2);
        return MonitorService.All().FirstOrDefault(m => m.Handle == handle) ?? MonitorService.ForPointer();
    }
    internal static PixelRect Constrain(PixelRect work, PixelRect requested, bool center)
    {
        var width = Math.Clamp(requested.Width, 1, Math.Max(1, work.Width - 24));
        var height = Math.Clamp(requested.Height, 1, Math.Max(1, work.Height - 24));
        var x = center ? work.X + (work.Width - width) / 2 : Math.Clamp(requested.X, work.X, work.Right - width);
        var y = center ? work.Y + (work.Height - height) / 2 : Math.Clamp(requested.Y, work.Y, work.Bottom - height);
        return new(x, y, width, height);
    }
    internal static void Activate(Window window)
    {
        // Raising a disabled owner leaves its modal dialog behind another app.
        var modal = window.OwnedWindows.Cast<Window>().LastOrDefault(w => w.IsVisible);
        if (modal is not null) { Activate(modal); return; }
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        if (!window.IsVisible) window.Show();
        window.Activate();
    }
}
