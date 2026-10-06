using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal sealed class DockWindow : Window
{
    private readonly AppController controller;
    private readonly StackPanel shelf = new() { Background = Brushes.Transparent };
    private readonly HashSet<Guid> selected = [];
    private readonly Dictionary<string, BitmapSource> thumbnails = [];
    private readonly DispatcherTimer hideTimer = new();
    private readonly DispatcherTimer collapseTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private MonitorInfo? anchor;
    private int configuredMonitor = int.MinValue;
    private bool positionQueued;
    private bool keyboardMode;
    private bool expanded;
    private bool dragging;
    private int index;
    private Point down;
    private CaptureRecord? pressed;
    private List<CaptureRecord> visible = [];
    internal int RebuildCount { get; private set; }
    internal int CardBuildCount { get; private set; }
    internal int ThumbnailDecodeCount { get; private set; }
    internal bool Expanded => expanded;
    internal int SelectionCount => selected.Count;
    internal void ToggleSelection(Guid id)
    {
        if (!selected.Add(id)) selected.Remove(id);
    }
    public DockWindow(AppController controller)
    {
        Ui.StyleWindow(this);
        this.controller = controller;
        Title = "SnippyGrab shelf"; WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        ShowActivated = false; Focusable = true; SizeToContent = SizeToContent.WidthAndHeight;
        Content = new Border { Background = Brushes.Transparent, Padding = new Thickness(4), Child = shelf };
        AutomationProperties.SetName(this, "Screenshot shelf. Ctrl-click to select multiple captures; drag to attach.");
        MouseEnter += (_, _) => { hideTimer.Stop(); collapseTimer.Stop(); SetExpanded(true); };
        MouseLeave += (_, _) => { collapseTimer.Stop(); collapseTimer.Start(); };
        collapseTimer.Tick += (_, _) =>
        {
            collapseTimer.Stop();
            if (dragging || pressed is not null || (keyboardMode && IsKeyboardFocusWithin) || HasOpenMenu() || PointerInside()) return;
            SetExpanded(!controller.Settings.AutoCollapse); ScheduleHide();
        };
        MouseWheel += (_, e) => { index = Math.Clamp(index + (e.Delta < 0 ? 1 : -1), 0, Math.Max(0, visible.Count - 1)); Rebuild(); e.Handled = true; };
        KeyDown += OnKey;
        hideTimer.Tick += (_, _) => { hideTimer.Stop(); if (!PointerInside() && !dragging && pressed is null && !HasOpenMenu() && !(keyboardMode && IsKeyboardFocusWithin)) Hide(); };
        SizeChanged += (_, _) => QueuePosition();
        LostKeyboardFocus += (_, _) => { if (!IsKeyboardFocusWithin) { keyboardMode = false; collapseTimer.Start(); } };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { hideTimer.Stop(); collapseTimer.Stop(); } };
        Closed += (_, _) => { hideTimer.Stop(); collapseTimer.Stop(); };
        Closing += (_, e) => { if (!controller.Exiting) { e.Cancel = true; Hide(); } };
    }
    public void Refresh(bool newCapture = false)
    {
        if (newCapture) { index = 0; selected.Clear(); keyboardMode = false; expanded = !controller.Settings.AutoCollapse; anchor = null; }
        if (configuredMonitor != controller.Settings.DockMonitor) { configuredMonitor = controller.Settings.DockMonitor; anchor = null; }
        anchor ??= MonitorService.ForPointer(controller.Settings.DockMonitor);
        visible = controller.Repository.Captures.Where(c => CaptureLifetime.Visible(c, controller.Settings.DockLifetimeMinutes, DateTimeOffset.UtcNow)).ToList();
        selected.RemoveWhere(id => visible.All(c => c.Id != id));
        var files = visible.Select(c => c.FileName).ToHashSet();
        foreach (var stale in thumbnails.Keys.Where(k => !files.Contains(k)).ToArray()) thumbnails.Remove(stale);
        index = Math.Clamp(index, 0, Math.Max(0, visible.Count - 1));
        Rebuild();
        if (visible.Count == 0) { Hide(); return; }
        if (newCapture)
        {
            Show(); Position(); ScheduleHide();
            if (controller.Settings.Animate && !SystemParameters.HighContrast)
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, controller.Settings.DockOpacity, TimeSpan.FromMilliseconds(130)));
        }
    }
    public void Reveal() { if (!IsVisible) anchor = null; Refresh(); if (visible.Count > 0) { Show(); UpdateLayout(); Position(); ScheduleHide(); } }
    internal void SetExpanded(bool value)
    {
        if (expanded == value) return;
        expanded = value; Rebuild();
    }
    private bool PointerInside()
    {
        if (!IsVisible) return false;
        Native.GetCursorPos(out var pointer); Native.GetWindowRect(new WindowInteropHelper(this).Handle, out var rect);
        return pointer.X >= rect.Left && pointer.X < rect.Right && pointer.Y >= rect.Top && pointer.Y < rect.Bottom;
    }
    private bool HasOpenMenu() => shelf.Children.OfType<Border>().Any(b => b.ContextMenu?.IsOpen == true);
    private void ScheduleHide()
    {
        hideTimer.Stop();
        if (controller.Settings.AutoHideSeconds > 0) { hideTimer.Interval = TimeSpan.FromSeconds(controller.Settings.AutoHideSeconds); hideTimer.Start(); }
    }
    private void Rebuild()
    {
        RebuildCount++;
        var restoreFocus = IsKeyboardFocusWithin;
        shelf.Children.Clear(); Topmost = controller.Settings.AlwaysOnTop;
        BeginAnimation(OpacityProperty, null); Opacity = controller.Settings.DockOpacity;
        shelf.Orientation = controller.Settings.Orientation == DockOrientation.Horizontal ? Orientation.Horizontal : Orientation.Vertical;
        if (visible.Count == 0) return;
        var count = expanded ? controller.Settings.ExpandedItems : 1;
        var monitor = anchor ??= MonitorService.ForPointer(controller.Settings.DockMonitor);
        var horizontal = shelf.Orientation == Orientation.Horizontal;
        var budget = DpiGeometry.ToDip(horizontal ? monitor.Work.Width : monitor.Work.Height, monitor.Dpi) * (horizontal ? 0.55 : 0.48);
        double used = 0;
        foreach (var capture in visible.Skip(index).Take(count))
        {
            var extent = (horizontal ? controller.Settings.ThumbnailSize : Math.Clamp(controller.Settings.ThumbnailSize * (double)capture.Height / Math.Max(1, capture.Width), 72, controller.Settings.ThumbnailSize * 0.65)) + 10;
            if (used > 0 && used + extent > budget) break;
            shelf.Children.Add(Card(capture)); used += extent;
        }
        if (!expanded && visible.Count > 1)
        {
            // Two understated offset edges communicate a stack without creating a gallery.
            for (var i = 0; i < Math.Min(2, visible.Count - 1); i++)
                shelf.Children.Add(new Border { Width = horizontal ? 4 : controller.Settings.ThumbnailSize - (i + 1) * 12, Height = horizontal ? 64 : 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromArgb((byte)(150 - i * 40), 81, 99, 124)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        }
        if (DockLayout.Reverse(controller.Settings.Orientation, controller.Settings.Corner))
        {
            var ordered = shelf.Children.Cast<UIElement>().Reverse().ToArray(); shelf.Children.Clear();
            foreach (var child in ordered) shelf.Children.Add(child);
        }
        if (restoreFocus) Keyboard.Focus(this);
    }
    private UIElement Card(CaptureRecord capture)
    {
        CardBuildCount++;
        var size = controller.Settings.ThumbnailSize;
        var grid = new Grid { Width = size, Height = Math.Clamp(size * (double)capture.Height / Math.Max(1, capture.Width), 72, size * 0.65), ClipToBounds = true };
        try
        {
            if (!thumbnails.TryGetValue(capture.FileName, out var thumb)) { thumbnails[capture.FileName] = thumb = ImageService.Load(controller.Repository.PathFor(capture), size * 2); ThumbnailDecodeCount++; }
            grid.Children.Add(new Image { Source = thumb, Stretch = Stretch.Uniform });
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or InvalidDataException or ArgumentException)
        { grid.Children.Add(Ui.Text("Image unavailable", 12)); }
        var border = new Border { Child = grid, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(2), BorderBrush = selected.Contains(capture.Id) ? (Brush)FindResource("Accent") : new SolidColorBrush(Color.FromArgb(90, 120, 136, 156)), Background = (Brush)FindResource("Surface"), Margin = new Thickness(3), Focusable = true, Tag = capture.Id };
        AutomationProperties.SetName(border, $"Screenshot {capture.Width} by {capture.Height}{(capture.Pinned ? ", pinned" : "")}. Click to edit; drag to attach.");
        var badge = Ui.Button($"{visible.IndexOf(capture) + 1}/{visible.Count}{(capture.Pinned ? " · pin" : "")}", "Open recent captures", controller.ShowHistory);
        badge.FontSize = 10; badge.Padding = new Thickness(6, 2, 6, 2); badge.HorizontalAlignment = HorizontalAlignment.Right; badge.VerticalAlignment = VerticalAlignment.Top; badge.Opacity = 0.9; grid.Children.Add(badge);
        if (expanded)
        {
            var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Background = (Brush)FindResource("Surface") };
            foreach (var (label, hint, action) in new (string, string, Action)[]
            {
                ("✎", "Edit (Enter)", () => controller.Edit(capture)),
                ("⧉", "Copy image (Ctrl+C)", () => controller.Run(() => controller.Copy(capture))),
                ("⌖", capture.Pinned ? "Unpin" : "Pin indefinitely", () => controller.Pin(capture)),
                ("↓", "Save as (Ctrl+S)", () => controller.Save(capture)),
                ("T", "OCR and copy text", () => controller.Run(() => controller.Ocr(capture))),
                ("×", "Dismiss (Delete)", () => controller.Dismiss([capture]))
            })
            { var b = Ui.Button(label, hint, action); b.Padding = new Thickness(size < 180 ? 2 : 6, 3, size < 180 ? 2 : 6, 3); b.Margin = new Thickness(1); b.FontSize = 13; controls.Children.Add(b); }
            grid.Children.Add(controls);
        }
        border.ContextMenu = Menu(capture);
        border.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (FindButton(e.OriginalSource as DependencyObject)) return;
            keyboardMode = false; down = e.GetPosition(this); pressed = capture; dragging = false;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                ToggleSelection(capture.Id);
                border.BorderBrush = (Brush)FindResource(selected.Contains(capture.Id) ? "Accent" : "Muted");
            }
            border.Focus(); border.CaptureMouse(); e.Handled = true;
        };
        border.MouseMove += (_, e) =>
        {
            if (pressed != capture || e.LeftButton != MouseButtonState.Pressed || dragging) return;
            var now = e.GetPosition(this);
            if (Math.Abs(now.X - down.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(now.Y - down.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            dragging = true;
            border.ReleaseMouseCapture();
            try
            {
                if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
                {
                    var data = new DataObject("SnippyGrab.Reorder", capture.Id.ToString()); DragDrop.DoDragDrop(border, data, DragDropEffects.Move);
                }
                else
                {
                    if (!selected.Contains(capture.Id)) { selected.Clear(); selected.Add(capture.Id); }
                    controller.DragDrop.Drag(border, visible.Where(c => selected.Contains(c.Id)).ToList());
                }
            }
            catch (Exception ex) { controller.Notify("Drag could not complete: " + ex.Message); }
            finally { pressed = null; dragging = false; Rebuild(); collapseTimer.Start(); }
        };
        border.MouseLeftButtonUp += (_, e) =>
        {
            var point = e.GetPosition(border);
            var inside = point.X >= 0 && point.Y >= 0 && point.X < border.ActualWidth && point.Y < border.ActualHeight;
            if (pressed == capture && !dragging && inside && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0) { selected.Clear(); selected.Add(capture.Id); controller.Edit(capture); }
            border.ReleaseMouseCapture(); collapseTimer.Start();
            pressed = null; e.Handled = true;
        };
        border.AllowDrop = true;
        border.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent("SnippyGrab.Reorder") ? DragDropEffects.Move : DragDropEffects.None; if (e.Effects == DragDropEffects.Move) { border.BorderBrush = (Brush)FindResource("Accent"); border.ToolTip = "Move screenshot to this position"; } e.Handled = true; };
        border.DragLeave += (_, _) => { border.BorderBrush = (Brush)FindResource(selected.Contains(capture.Id) ? "Accent" : "Muted"); border.ToolTip = null; };
        border.Drop += (_, e) =>
        {
            if (e.Data.GetData("SnippyGrab.Reorder") is string id && Guid.TryParse(id, out var guid)) controller.Reorder(guid, capture.Id);
            e.Handled = true;
        };
        return border;
    }
    private static bool FindButton(DependencyObject? source)
    {
        while (source is not null) { if (source is Button) return true; source = VisualTreeHelper.GetParent(source); }
        return false;
    }
    private ContextMenu Menu(CaptureRecord record)
    {
        var menu = new ContextMenu();
        foreach (var (name, action) in new (string, Action)[]
        {
            ("Edit", () => controller.Edit(record)), ("Copy image", () => controller.Run(() => controller.Copy(record))),
            ("Copy file(s)", () => controller.Run(() => controller.CopyFiles(SelectedOr(record)))),
            ("Copy file path", () => controller.Run(() => controller.Clipboard.TextAsync(controller.Repository.PathFor(record)))),
            ("Copy filename", () => controller.Run(() => controller.Clipboard.TextAsync(record.FileName))),
            ("Copy OCR text", () => controller.Run(() => controller.Ocr(record))), ("Save as…", () => controller.Save(record)),
            (record.Pinned ? "Unpin" : "Pin indefinitely", () => controller.Pin(record)), ("Detach pin", () => controller.Detach(record)),
            ("Dismiss", () => controller.Dismiss(SelectedOr(record))), ("Recent captures", controller.ShowHistory)
        }) { var item = new MenuItem { Header = name }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        return menu;
    }
    private List<CaptureRecord> SelectedOr(CaptureRecord record) => selected.Contains(record.Id) ? visible.Where(c => selected.Contains(c.Id)).ToList() : [record];
    private void OnKey(object sender, KeyEventArgs e)
    {
        keyboardMode = true; hideTimer.Stop(); collapseTimer.Stop();
        var current = visible.ElementAtOrDefault(index); if (current is null) return;
        if (e.Key == Key.Delete) controller.Dismiss(SelectedOr(current));
        else if (e.Key == Key.Enter) controller.Edit(current);
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control) controller.Run(() => selected.Count > 1 ? controller.CopyFiles(SelectedOr(current)) : controller.Copy(current));
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) controller.Save(current);
        else if (e.Key == Key.Escape) { selected.Clear(); expanded = false; Rebuild(); }
        else if (e.Key is Key.Up or Key.Left or Key.Down or Key.Right) { index = Math.Clamp(index + (e.Key is Key.Up or Key.Left ? -1 : 1), 0, visible.Count - 1); Rebuild(); }
        else return;
        e.Handled = true;
    }
    private void Position()
    {
        if (!IsVisible) return;
        var monitor = anchor ??= MonitorService.ForPointer(controller.Settings.DockMonitor);
        // SetWindowPos uses physical origin; only size is converted with the destination monitor DPI.
        var width = DpiGeometry.ToPixel(ActualWidth, monitor.Dpi); var height = DpiGeometry.ToPixel(ActualHeight, monitor.Dpi);
        var placement = DockLayout.Place(monitor.Work, width, height, controller.Settings.Corner);
        Native.SetWindowPos(new WindowInteropHelper(this).Handle, controller.Settings.AlwaysOnTop ? -1 : -2,
            placement.X, placement.Y, placement.Width, placement.Height, 0x0010);
    }
    private void QueuePosition()
    {
        if (positionQueued) return;
        positionQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () => { positionQueued = false; Position(); });
    }
}
