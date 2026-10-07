using System.Text.Json;
using SnippyGrab.App.Views;

namespace SnippyGrab.App.Services;

internal static class InteractiveChecks
{
    internal static bool TargetableWindows { get; private set; }
    // Opt-in, isolated visual/gesture lane. Never loads normal settings or captures.
    public static AppController Open(string report)
    {
        TargetableWindows = true;
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-interactive-" + Guid.NewGuid().ToString("N"));
        new SettingsService(Path.Combine(root, "settings.json")).Save(new Settings { FirstRunComplete = true, Animate = false, AutoCopy = false, ExpandedItems = 5, DockLifetimeMinutes = 0, SaveDirectory = Path.GetDirectoryName(Path.GetFullPath(report))! });
        var controller = new AppController(true, root, diagnostic: true);
        controller.Dock.ShowInTaskbar = true; // Computer-use inventory excludes ordinary tool windows.
        var clipboard = new List<string>();
        controller.Clipboard = new ClipboardService(data => { clipboard.Add(data.GetDataPresent(DataFormats.FileDrop) ? "files" : data.GetDataPresent(DataFormats.Bitmap) ? "image" : "text"); Save(); }, _ => Task.CompletedTask);
        BitmapSource Fixture(int number)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 720, 360));
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb((byte)(number * 11), 70, 160)), null, new Rect(20, 20, 680, 8));
                dc.DrawText(new System.Windows.Media.FormattedText($"CAPTURE {number:00}\nBuild failed\nError CS1002: semicolon expected\nTraceback: ValueError: invalid literal", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), 28, Brushes.Black, 1), new Point(30, 55));
            }
            var image = new RenderTargetBitmap(720, 360, 96, 96, PixelFormats.Pbgra32); image.Render(visual); image.Freeze();
            return image;
        }
        var bitmap = Fixture(1);
        for (var i = 20; i >= 1; i--) controller.Repository.Add(ImageService.Png(Fixture(i)), 720, 360);
        var window = new Window { Title = "SnippyGrab · Isolated interaction checks", Width = 760, Height = 430, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        Ui.StyleWindow(window);
        var panel = new StackPanel { Margin = new Thickness(20) }; window.Content = panel;
        panel.Children.Add(Ui.Text("Isolated interaction checks", 24));
        panel.Children.Add(new TextBlock { Text = "Synthetic CAPTURE 01–20 · hotkeys disabled · clipboard intercepted\nActual dialogs, pointer gestures and WPF keyboard handlers.\nCapture buttons use the real desktop; files stay in the isolated temporary cache.\nWindows startup and custom cache paths are disabled in this fixture.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) });
        var buttons = new WrapPanel(); panel.Children.Add(buttons);
        void Button(string label, Action action) => buttons.Children.Add(Ui.Button(label, label, () => { action(); Save(); }));
        foreach (var count in new[] { 1, 3, 5, 20 })
            Button(count + " captures", () => { for (var i = 0; i < controller.Repository.Captures.Count; i++) controller.Repository.Captures[i].Dismissed = i >= count; controller.Repository.Persist(); controller.Dock.Refresh(true); });
        Button("Focus shelf", controller.Dock.FocusShelf);
        Button("Edit first", () => controller.Edit(controller.Repository.Captures[0]));
        Button("Edit second", () => controller.Edit(controller.Repository.Captures[1]));
        Button("History", controller.ShowHistory);
        Button("Detached pin", () => controller.Detach(controller.Repository.Captures[0]));
        Button("Settings", controller.ShowSettings);
        foreach (var mode in Enum.GetValues<CaptureMode>()) Button("Capture " + mode, () => controller.Run(() => controller.Capture(mode)));
        Button("Hide shelf", controller.Dock.Hide);
        Button("Display change check", controller.Dock.TopologyChanged);
        Button("Exit checks", controller.Exit);
        panel.Children.Add(new Image { Source = bitmap, Height = 180, Stretch = Stretch.Uniform, Margin = new Thickness(0, 12, 0, 0) });
        controller.Repository.Changed += Save;
        window.Closed += (_, _) => controller.Exit();
        window.Show(); controller.Dock.Refresh(true); Save();
        return controller;
        void Save() => AtomicFile.Write(report, JsonSerializer.SerializeToUtf8Bytes(new { Build = BuildVersion.Display, Scope = "isolated interactive fixture; clipboard intercepted; receiver delivery requires separate real-app acceptance", Root = root, ClipboardOperations = clipboard.ToArray(), Captures = controller.Repository.Captures.Select(c => new { c.Id, c.Width, c.Height, c.Edited, c.Pinned, c.Dismissed, Exported = c.ExportPath.Length > 0 }).ToArray(), Monitors = MonitorService.All().Select(m => new { m.Index, m.Bounds, m.Work, m.Dpi }), controller.Dock.IsVisible }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
