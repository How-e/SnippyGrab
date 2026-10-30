using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Interop;
using SnippyGrab.App.Views;

namespace SnippyGrab.App.Services;

internal static class WindowLayoutChecks
{
    internal static async Task Run(string report)
    {
        var results = new List<object>();
        foreach (var monitor in MonitorService.All())
        {
            var owner = new Window { Width = 400, Height = 240, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000 };
            owner.Show();
            Native.SetWindowPos(new WindowInteropHelper(owner).Handle, 0, monitor.Work.X + 30, monitor.Work.Y + 30, Math.Min(400, monitor.Work.Width), Math.Min(240, monitor.Work.Height), 0x0014);
            var window = new Window { Title = "SnippyGrab · Window layout fixture", Owner = owner, Width = 1320, Height = 900, MinWidth = 800, MinHeight = 550, ShowActivated = false, ShowInTaskbar = false, Content = new TextBlock { Text = "TITLE / TOP EDGE\nNative monitor layout fixture", Margin = new Thickness(16) } };
            Ui.StyleWindow(window);
            try
            {
                window.Show(); await Task.Delay(120); window.UpdateLayout();
                var hwnd = new WindowInteropHelper(window).Handle;
                Native.GetWindowRect(hwnd, out var normal);
                Require(normal.Pixels.Intersect(monitor.Work) == normal.Pixels, $"Normal window {normal.Pixels} exceeded the owner's work area {monitor.Work}; owner monitor {WindowPlacement.ForWindow(owner).Work}.");
                Require(Math.Abs(normal.Left - (monitor.Work.X + (monitor.Work.Width - normal.Pixels.Width) / 2)) <= 1, "Window was not centered on the owner's monitor.");
                Require(WindowPlacement.ForWindow(window).Handle == monitor.Handle, "Window opened on another monitor.");
                window.WindowState = WindowState.Maximized; await Task.Delay(120); window.UpdateLayout();
                Native.DwmGetWindowAttribute(hwnd, 9, out var visible, Marshal.SizeOf<Native.RECT>());
                Require(visible.Top >= monitor.Work.Y - 1 && visible.Bottom <= monitor.Work.Bottom + 1 && visible.Left >= monitor.Work.X - 1 && visible.Right <= monitor.Work.Right + 1, "Maximized visible frame cut into the taskbar or another monitor.");
                Require(((FrameworkElement)window.Content).ActualHeight > 0, "Maximized content disappeared.");
                window.WindowState = WindowState.Normal; await Task.Delay(80);
                Native.GetWindowRect(hwnd, out var restored);
                Require(restored.Pixels.Intersect(monitor.Work) == restored.Pixels, "Restored window exceeded the work area.");
                window.WindowState = WindowState.Minimized; WindowPlacement.Activate(window); await Task.Delay(80);
                Require(window.WindowState == WindowState.Normal, "Reopening a minimized window did not restore it.");
                results.Add(new { monitor.Index, monitor.Bounds, monitor.Work, monitor.Dpi, Normal = normal.Pixels, MaximizedVisible = visible.Pixels, Restored = restored.Pixels });
            }
            finally { window.Close(); owner.Close(); }
        }
        Require(results.Count > 0, "No monitors available for native layout verification.");
        await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { Result = "PASS", Monitors = results, Scope = "Native WPF owner-monitor placement, work-area bounds, maximize visible frame, restore and minimized-window activation on every attached monitor. Synthetic geometry tests cover negative origins and taskbar offsets; unavailable display arrangements and physical gestures remain separate." }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
