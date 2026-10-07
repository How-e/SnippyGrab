using System.Windows.Interop;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal sealed class PinWindow : Window
{
    private readonly AppController controller;
    private readonly CaptureRecord record;
    private readonly Image preview;
    private readonly CaptureViewLease lease;
    private bool clickThrough;
    private bool ready;
    private readonly System.Windows.Threading.DispatcherTimer resizeTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    public PinWindow(AppController controller, CaptureRecord record)
    {
        Ui.StyleWindow(this);
        this.controller = controller; this.record = record;
        Title = "SnippyGrab pin"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; Width = 320; Height = 220; MinWidth = 80; MinHeight = 60; ResizeMode = ResizeMode.CanResizeWithGrip;
        preview = new Image { Source = LoadPreview(), Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.HighQuality);
        Content = new Border { Child = preview, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), BorderBrush = (Brush)FindResource("Muted"), Background = (Brush)FindResource("Surface") };
        MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) controller.Edit(record); else DragMove(); };
        var menu = new ContextMenu();
        foreach (var (label, action) in new (string, Action)[]
        {
            ("Copy", () => controller.Run(() => controller.Copy(record))), ("Edit", () => controller.Edit(record)),
            ("Toggle always on top", () => { Topmost = !Topmost; SaveLayout(); }),
            ("Click-through (restore via tray → Restore pins)", () => { SetClickThrough(true); SaveLayout(); }),
            ("Return to shelf", () => { controller.Repository.Restore([record], DateTimeOffset.UtcNow); controller.Dock.Reveal(); Close(); }), ("Close pin window", Close)
        }) { var item = new MenuItem { Header = label }; item.Click += (_, _) => controller.Try(action); menu.Items.Add(item); }
        var opacity = new Slider { Minimum = 0.25, Maximum = 1, Value = record.PinLayout?.Normalize().Opacity ?? 1, Width = 160, SmallChange = 0.05, LargeChange = 0.1, ToolTip = "Pin opacity (25–100%)" };
        System.Windows.Automation.AutomationProperties.SetName(opacity, "Pin opacity");
        opacity.ValueChanged += (_, _) => { Opacity = opacity.Value; ScheduleLayout(); };
        menu.Items.Insert(3, new MenuItem { Header = opacity, StaysOpenOnClick = true });
        ContextMenu = menu;
        lease = new(controller.Repository, record);
        controller.Repository.RevisionChanged += RevisionChanged;
        controller.SettingsChanged += RefreshPreview;
        resizeTimer.Tick += (_, _) => { resizeTimer.Stop(); RefreshPreview(); SaveLayout(); };
        SizeChanged += (_, _) => { resizeTimer.Stop(); resizeTimer.Start(); };
        LocationChanged += (_, _) => ScheduleLayout();
        Loaded += (_, _) => RestoreLayout();
        DpiChanged += (_, e) => { if (ReferenceEquals(e.Source, this)) { resizeTimer.Stop(); resizeTimer.Start(); } };
        Closing += (_, _) => SaveLayout();
        Closed += (_, _) => { ready = false; resizeTimer.Stop(); controller.SettingsChanged -= RefreshPreview; controller.Repository.RevisionChanged -= RevisionChanged; lease.Dispose(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
    private void RevisionChanged(CaptureRecord changed)
    {
        if (changed.Id == record.Id) controller.Try(() => preview.Source = LoadPreview());
    }
    public void RestoreInteraction()
    {
        SetClickThrough(false); SaveLayout();
        Show(); Activate(); preview.Source = LoadPreview();
    }
    private void SetClickThrough(bool enabled)
    {
        clickThrough = enabled;
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = Native.GetWindowLongPtr(hwnd, -20);
        Native.SetWindowLongPtr(hwnd, -20, enabled ? style | 0x20 | 0x08000000 : style & ~((nint)0x20 | 0x08000000));
    }
    private void ScheduleLayout() { if (ready) { resizeTimer.Stop(); resizeTimer.Start(); } }
    private void RestoreLayout()
    {
        if (record.PinLayout is { } saved)
        {
            var layout = saved.Normalize(); var monitors = MonitorService.All();
            var monitor = monitors.FirstOrDefault(m => m.Identity == layout.MonitorIdentity) ?? monitors.FirstOrDefault(m => m.Primary) ?? monitors[0];
            var bounds = layout.Place(monitor.Work, monitor.Dpi);
            Width = DpiGeometry.ToDip(bounds.Width, monitor.Dpi); Height = DpiGeometry.ToDip(bounds.Height, monitor.Dpi);
            Topmost = layout.Topmost; Opacity = layout.Opacity;
            Native.SetWindowPos(new WindowInteropHelper(this).Handle, 0, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0014);
            SetClickThrough(layout.ClickThrough);
        }
        ready = true;
    }
    private void SaveLayout()
    {
        if (!ready) return;
        controller.Try(() =>
        {
            if (!Native.GetWindowRect(new WindowInteropHelper(this).Handle, out var rect)) return;
            var monitor = MonitorService.All().OrderByDescending(m => m.Bounds.Intersect(rect.Pixels).Width * (long)m.Bounds.Intersect(rect.Pixels).Height).First();
            var layout = new PinLayout(monitor.Identity, DpiGeometry.ToDip(rect.Left - monitor.Work.X, monitor.Dpi), DpiGeometry.ToDip(rect.Top - monitor.Work.Y, monitor.Dpi),
                DpiGeometry.ToDip(rect.Pixels.Width, monitor.Dpi), DpiGeometry.ToDip(rect.Pixels.Height, monitor.Dpi), Opacity, Topmost, clickThrough);
            if (record.PinLayout != layout) controller.Repository.SetPinLayout(record, layout);
        });
    }
    private void RefreshPreview() => controller.Try(() => preview.Source = LoadPreview());
    private BitmapSource LoadPreview()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = ImageService.PreviewPixels((int)Math.Ceiling(Math.Max(800, ActualWidth * dpi.DpiScaleX)), controller.Settings.PreviewQuality);
        var height = ImageService.PreviewPixels((int)Math.Ceiling(Math.Max(800, ActualHeight * dpi.DpiScaleY)), controller.Settings.PreviewQuality);
        return ImageService.Load(controller.Repository.PathFor(record), width, height);
    }
}
