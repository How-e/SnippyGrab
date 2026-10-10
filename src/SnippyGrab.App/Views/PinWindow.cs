using System.Windows.Interop;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal sealed class PinWindow : Window
{
    private readonly AppController controller;
    private readonly CaptureRecord record;
    internal Image PreviewImage => preview;
    internal Task PreviewWork { get; private set; } = Task.CompletedTask;
    internal Task DrainPreviewAsync() => previewLoader.DrainAsync();
    private readonly Image preview;
    private readonly CaptureViewLease lease;
    private readonly PreviewLoader previewLoader = new();
    private (string File, int Width, int Height)? previewStamp;
    private bool clickThrough;
    private bool ready;
    private bool closed;
    private readonly System.Windows.Threading.DispatcherTimer resizeTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    public PinWindow(AppController controller, CaptureRecord record)
    {
        Ui.StyleWindow(this);
        this.controller = controller; this.record = record;
        Title = "SnippyGrab pin"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; Width = 320; Height = 220; MinWidth = 80; MinHeight = 60; ResizeMode = ResizeMode.CanResizeWithGrip;
        preview = new Image { Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.HighQuality);
        var frame = new Grid(); frame.Children.Add(preview);
        var chrome = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Collapsed };
        chrome.SetResourceReference(Panel.BackgroundProperty, "Raised");
        chrome.Children.Add(Ui.ActionButton("Copy", "copy", "Copy pinned image", () => controller.Run(() => controller.Copy(record)), "QuietButton"));
        chrome.Children.Add(Ui.ActionButton("Edit", "edit", "Edit pinned image", () => controller.Edit(record), "QuietButton"));
        chrome.Children.Add(Ui.IconButton("more", "Pinned image actions", () => { ContextMenu.PlacementTarget = this; ContextMenu.IsOpen = true; }));
        frame.Children.Add(chrome);
        SizeChanged += (_, _) => { foreach (var button in chrome.Children.OfType<Button>().Take(2)) button.Visibility = ActualWidth < 220 * Ui.TextScale ? Visibility.Collapsed : Visibility.Visible; };
        var border = new Border { Child = frame, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1) };
        border.SetResourceReference(Border.BorderBrushProperty, "Border"); border.SetResourceReference(Border.BackgroundProperty, "Surface"); Content = border;
        MouseEnter += (_, _) => chrome.Visibility = Visibility.Visible;
        MouseLeave += (_, _) => { if (!ContextMenu.IsOpen) chrome.Visibility = Visibility.Collapsed; };
        MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is DependencyObject source && FindButton(source)) return; if (e.ClickCount == 2) controller.Edit(record); else DragMove(); };
        var menu = new ContextMenu();
        menu.Items.Add(Ui.Menu("Copy", "copy", () => controller.Run(() => controller.Copy(record))));
        menu.Items.Add(Ui.Menu("Edit", "edit", () => controller.Edit(record)));
        menu.Items.Add(new Separator());
        var onTop = Ui.Menu("Always on top", "pin", () => { Topmost = !Topmost; SaveLayout(); }); onTop.IsCheckable = true;
        menu.Items.Add(onTop);
        var opacity = new Slider { Minimum = 0.25, Maximum = 1, Value = record.PinLayout?.Normalize().Opacity ?? 1, Width = 160, SmallChange = 0.05, LargeChange = 0.1, ToolTip = "Pin opacity (25–100%)" };
        System.Windows.Automation.AutomationProperties.SetName(opacity, "Pin opacity");
        opacity.ValueChanged += (_, _) => { Opacity = opacity.Value; ScheduleLayout(); };
        var opacityRow = new StackPanel(); var opacityLabel = Ui.Text("Opacity 96%", 12, true); opacityRow.Children.Add(opacityLabel); opacityRow.Children.Add(opacity);
        opacity.ValueChanged += (_, _) => opacityLabel.Text = $"Opacity {opacity.Value:P0}";
        menu.Items.Add(new MenuItem { Header = opacityRow, StaysOpenOnClick = true });
        var through = Ui.Menu("Click-through", "capture", () => { SetClickThrough(!clickThrough); SaveLayout(); }); through.IsCheckable = true; through.ToolTip = "Restore interaction through tray → Restore pins."; menu.Items.Add(through);
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Menu("Return to shelf", "left", () => { controller.Repository.Restore([record], DateTimeOffset.UtcNow); controller.Dock.Reveal(); Close(); }));
        menu.Items.Add(Ui.Menu("Close pin window", "close", Close));
        menu.Opened += (_, _) => { onTop.IsChecked = Topmost; through.IsChecked = clickThrough; opacity.Value = Opacity; opacityLabel.Text = $"Opacity {Opacity:P0}"; };
        menu.Closed += (_, _) => { if (!IsMouseOver) chrome.Visibility = Visibility.Collapsed; };
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
        Closed += (_, _) => { closed = true; ready = false; resizeTimer.Stop(); controller.SettingsChanged -= RefreshPreview; controller.Repository.RevisionChanged -= RevisionChanged; previewLoader.Dispose(); preview.Source = null; lease.Dispose(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        RefreshPreview();
    }
    private static bool FindButton(DependencyObject? source)
    {
        while (source is not null) { if (source is Button) return true; source = VisualTreeHelper.GetParent(source); }
        return false;
    }
    private void RevisionChanged(CaptureRecord changed)
    {
        if (changed.Id == record.Id) RefreshPreview();
    }
    public void RestoreInteraction()
    {
        SetClickThrough(false); SaveLayout();
        Show(); Activate(); RefreshPreview();
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
    private void RefreshPreview()
    {
        if (closed) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = ImageService.PreviewPixels((int)Math.Ceiling(Math.Max(800, ActualWidth * dpi.DpiScaleX)), controller.Settings.PreviewQuality);
        var height = ImageService.PreviewPixels((int)Math.Ceiling(Math.Max(800, ActualHeight * dpi.DpiScaleY)), controller.Settings.PreviewQuality);
        var path = controller.Repository.PathFor(record);
        var stamp = (record.FileName, width, height);
        if (previewStamp == stamp && (preview.Source is not null || !PreviewWork.IsCompleted)) return;
        if (previewStamp?.File != record.FileName) preview.Source = null;
        previewStamp = stamp;
        PreviewWork = LoadPreviewAsync(path, width, height); controller.Run(() => PreviewWork);
    }
    private async Task LoadPreviewAsync(string path, int width, int height)
    {
        using var pendingLease = controller.Repository.Lease([record]);
        try { preview.Source = await previewLoader.LoadAsync(path, width, height); }
        catch (OperationCanceledException) { }
    }
}
