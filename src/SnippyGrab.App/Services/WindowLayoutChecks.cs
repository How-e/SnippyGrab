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
            await CheckDesignMonitor(monitor);
        }
        Require(results.Count > 0, "No monitors available for native layout verification.");
        await CheckWorkspaceReuse();
        await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { Result = "PASS", Monitors = results, WorkspaceReuse = true, Scope = "Native WPF owner-monitor placement, work-area bounds, maximize visible frame, restore and minimized-window activation on every attached monitor; repeated workspace launch, saved-design identity, empty-workspace reuse, distinct-document preservation and singleton restoration. Synthetic geometry tests cover negative origins and taskbar offsets; unavailable display arrangements and physical gestures remain separate." }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static async Task CheckDesignMonitor(MonitorInfo monitor)
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-design-monitor-" + Guid.NewGuid().ToString("N"));
        var window = new DesignWindow(root, (_, _, _, _) => Task.CompletedTask, _ => Task.FromResult(true), diagnostic: true, initialMonitor: monitor) { ShowActivated = false };
        try
        {
            window.Show();
            var deadline = DateTime.UtcNow.AddSeconds(25);
            while (!window.Ready) { Require(DateTime.UtcNow < deadline, "Design workspace did not initialize for monitor checks."); await Task.Delay(25); }
            Require(WindowPlacement.ForWindow(window).Handle == monitor.Handle, "Design workspace lost the launch monitor after asynchronous preparation.");
            window.WindowState = WindowState.Maximized; await Task.Delay(120);
            using var layout = JsonDocument.Parse(await window.Editor.CoreWebView2.ExecuteScriptAsync("({top:document.querySelector('header').getBoundingClientRect().top,bottom:document.querySelector('footer').getBoundingClientRect().bottom,height:innerHeight})"));
            Require(layout.RootElement.GetProperty("top").GetDouble() >= 0 && layout.RootElement.GetProperty("bottom").GetDouble() <= layout.RootElement.GetProperty("height").GetDouble() + 1, "Maximized design workspace clipped its header or footer.");
        }
        finally { window.CloseForDiagnostics(); }
    }
    private static async Task CheckWorkspaceReuse()
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-window-check-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "settings.json"), JsonSerializer.Serialize(new Settings { FirstRunComplete = true, StartMinimized = true }));
        using var controller = new AppController(true, root, diagnostic: true);
        DesignWindow[] Workspaces() => Application.Current.Windows.OfType<DesignWindow>().ToArray();
        try
        {
            controller.ShowDesign(); controller.ShowDesign();
            Require(Workspaces().Length == 1, "Repeated UI Design launch created duplicate workspaces.");
            var first = Workspaces().Single(); first.WindowState = WindowState.Minimized; controller.ShowDesign();
            Require(first.WindowState == WindowState.Normal, "Existing workspace was left minimized.");
            var doc = new DesignDocument { Width = 160, Height = 100 }; doc.SealBaseline();
            var path = Path.Combine(root, "fixture.snippy"); DesignStore.Save(path, doc);
            controller.ShowDesign(doc, path); controller.ShowDesign(DesignStore.Load(path), path);
            Require(Workspaces().Length == 1 && first.Session?.Document.Id == doc.Id, "Opening a saved design duplicated its existing or empty workspace.");
            var copyPath = Path.Combine(root, "separate-copy.snippy"); DesignStore.Save(copyPath, doc); controller.ShowDesign(DesignStore.Load(copyPath), copyPath);
            Require(Workspaces().Length == 2, "A separate saved copy was silently replaced by the workspace for another path.");
            var second = new DesignDocument { Width = 200, Height = 120 }; second.SealBaseline(); controller.ShowDesign(second);
            Require(Workspaces().Length == 3 && first.Session?.Document.Id == doc.Id, "Another design replaced an existing document.");
            controller.ShowSettings(); var settings = Application.Current.Windows.OfType<SettingsWindow>().Single(); settings.WindowState = WindowState.Minimized; controller.ShowSettings();
            Require(settings.WindowState == WindowState.Normal, "Settings remained minimized after reopening."); settings.Close();
            controller.ShowHistory(); var history = Application.Current.Windows.OfType<HistoryWindow>().Single(); history.WindowState = WindowState.Minimized; controller.ShowHistory();
            Require(history.WindowState == WindowState.Normal, "Recent captures remained minimized after reopening."); history.Close();
            await Task.Delay(100);
        }
        finally { foreach (var workspace in Workspaces()) workspace.CloseForDiagnostics(); controller.Dock.Close(); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
