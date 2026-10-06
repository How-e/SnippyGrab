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
    private readonly ShelfSelection selection = new();
    private HashSet<Guid> selected => selection.Selected;
    private readonly BoundedCache<(string File, int Pixels), BitmapSource> thumbnails = new(12);
    private readonly BoundedCache<CardStamp, CardVisual> cards = new(5);
    private sealed record CardStamp(Guid Id, string File, int Width, int Height, int Size, int Number, int Total, bool Pinned, Brush Surface, Brush Accent);
    private sealed record CardVisual(Border Frame, StackPanel Controls, TextBlock State, bool Available);
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
    internal int CachedThumbnailCount => thumbnails.Count;
    internal int CachedCardCount => cards.Count;
    internal Guid? FocusedCapture => selection.Focused;
    internal Action<ShelfAction, IReadOnlyList<CaptureRecord>>? CommandSinkOverride { get; set; }
    internal void ToggleSelection(Guid id)
    {
        selection.Toggle(id);
    }
    public DockWindow(AppController controller)
    {
        Ui.StyleWindow(this);
        this.controller = controller;
        Title = "SnippyGrab shelf"; WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        ShowActivated = false; Focusable = true; SizeToContent = SizeToContent.WidthAndHeight;
        // Layered-window pixels with alpha zero are click-through, even when WPF hit testing succeeds.
        // Keep padding/gaps almost invisible but nonzero even at the minimum configured opacity.
        Content = new Border { Background = new SolidColorBrush(Color.FromArgb(8, 0, 0, 0)), Padding = new Thickness(4), Child = shelf };
        AutomationProperties.SetName(this, "Screenshot shelf. Use the tray Focus shelf action or click a numbered badge for keyboard focus. Arrows navigate; Space selects; Enter edits; Delete dismisses.");
        KeyboardNavigation.SetTabNavigation(shelf, KeyboardNavigationMode.Cycle);
        MouseEnter += (_, _) => { hideTimer.Stop(); collapseTimer.Stop(); SetExpanded(true); };
        MouseLeave += (_, _) => { collapseTimer.Stop(); collapseTimer.Start(); };
        collapseTimer.Tick += (_, _) =>
        {
            collapseTimer.Stop();
            if (dragging || pressed is not null || (keyboardMode && IsKeyboardFocusWithin) || HasOpenMenu() || PointerInside()) return;
            SetExpanded(!controller.Settings.AutoCollapse); ScheduleHide();
        };
        MouseWheel += (_, e) => { Scroll(e.Delta < 0 ? 1 : -1); e.Handled = true; };
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
        if (newCapture) { index = 0; selection.Reset(); keyboardMode = false; expanded = !controller.Settings.AutoCollapse; anchor = null; }
        if (configuredMonitor != controller.Settings.DockMonitor) { configuredMonitor = controller.Settings.DockMonitor; anchor = null; }
        anchor ??= MonitorService.ForPointer(controller.Settings.DockMonitor);
        visible = controller.Repository.Captures.Where(c => CaptureLifetime.Visible(c, controller.Settings.DockLifetimeMinutes, DateTimeOffset.UtcNow)).ToList();
        selection.Update(visible, visible.ElementAtOrDefault(index)?.Id);
        var files = visible.Select(c => c.FileName).ToHashSet();
        thumbnails.RemoveWhere(k => !files.Contains(k.File));
        cards.RemoveWhere(k => !files.Contains(k.File));
        index = Math.Clamp(index, 0, Math.Max(0, visible.Count - 1));
        Rebuild();
        if (visible.Count == 0) { Hide(); return; }
        if (newCapture)
        {
            Show(); Position(); ScheduleHide();
            if (controller.Settings.Animate && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast)
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, controller.Settings.DockOpacity, TimeSpan.FromMilliseconds(130)));
        }
    }
    public void Reveal() { if (!IsVisible) anchor = null; Refresh(); if (visible.Count > 0) { Show(); UpdateLayout(); Position(); ScheduleHide(); } }
    public void FocusShelf() { Reveal(); if (selection.Current is { } current) FocusCapture(current.Id); }
    private void FocusCapture(Guid id)
    {
        selection.Focus(id); keyboardMode = true; hideTimer.Stop(); collapseTimer.Stop();
        EnsureFocusedVisible(); Activate(); FocusCurrentCard();
    }
    private void EnsureFocusedVisible()
    {
        var ordinal = visible.FindIndex(c => c.Id == selection.Focused);
        if (ordinal < 0) return;
        if (!shelf.Children.OfType<Border>().Any(b => b.Tag is Guid id && id == selection.Focused)) index = ordinal;
        expanded = true; Rebuild();
    }
    private void FocusCurrentCard()
    {
        var card = shelf.Children.OfType<Border>().FirstOrDefault(b => b.Tag is Guid id && id == selection.Focused);
        card?.Focus();
    }
    internal void SetExpanded(bool value)
    {
        if (expanded == value) return;
        expanded = value; Rebuild();
    }
    internal void Scroll(int offset)
    {
        var next = Math.Clamp(index + offset, 0, Math.Max(0, visible.Count - 1));
        if (next == index) return;
        index = next; Rebuild();
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
        Topmost = controller.Settings.AlwaysOnTop;
        var children = new List<UIElement>();
        BeginAnimation(OpacityProperty, null); Opacity = controller.Settings.DockOpacity;
        shelf.Orientation = controller.Settings.Orientation == DockOrientation.Horizontal ? Orientation.Horizontal : Orientation.Vertical;
        if (visible.Count == 0) { shelf.Children.Clear(); return; }
        var count = expanded ? controller.Settings.ExpandedItems : 1;
        var monitor = anchor ??= MonitorService.ForPointer(controller.Settings.DockMonitor);
        var horizontal = shelf.Orientation == Orientation.Horizontal;
        var budget = DpiGeometry.ToDip(horizontal ? monitor.Work.Width : monitor.Work.Height, monitor.Dpi) * (horizontal ? 0.55 : 0.48);
        double used = 0;
        foreach (var capture in visible.Skip(index).Take(count))
        {
            var extent = (horizontal ? controller.Settings.ThumbnailSize : Math.Clamp(controller.Settings.ThumbnailSize * (double)capture.Height / Math.Max(1, capture.Width), 72, controller.Settings.ThumbnailSize * 0.65)) + 10;
            if (used > 0 && used + extent > budget) break;
            children.Add(Card(capture)); used += extent;
        }
        if (!expanded && visible.Count > 1)
        {
            // Two understated offset edges communicate a stack without creating a gallery.
            for (var i = 0; i < Math.Min(2, visible.Count - 1); i++)
                children.Add(new Border { Width = horizontal ? 4 : controller.Settings.ThumbnailSize - (i + 1) * 12, Height = horizontal ? 64 : 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromArgb((byte)(150 - i * 40), 81, 99, 124)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        }
        if (DockLayout.Reverse(controller.Settings.Orientation, controller.Settings.Corner)) children.Reverse();
        if (!shelf.Children.Cast<UIElement>().SequenceEqual(children))
        {
            shelf.Children.Clear(); foreach (var child in children) shelf.Children.Add(child);
        }
        if (restoreFocus) FocusCurrentCard();
    }
    private UIElement Card(CaptureRecord capture)
    {
        var stamp = new CardStamp(capture.Id, capture.FileName, capture.Width, capture.Height, controller.Settings.ThumbnailSize, visible.IndexOf(capture) + 1, visible.Count, capture.Pinned, (Brush)FindResource("Surface"), (Brush)FindResource("Accent"));
        if (cards.TryGetValue(stamp, out var old) && !old.Available) cards.Remove(stamp);
        var card = cards.GetOrAdd(stamp, () => CreateCard(capture));
        card.Frame.BorderBrush = (Brush)FindResource(selected.Contains(capture.Id) ? "Accent" : "Muted");
        card.Frame.HorizontalAlignment = controller.Settings.Corner is DockCorner.TopRight or DockCorner.BottomRight ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        card.Frame.VerticalAlignment = controller.Settings.Corner is DockCorner.BottomLeft or DockCorner.BottomRight ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        var focused = keyboardMode && selection.Focused == capture.Id;
        card.State.Text = focused ? selected.Contains(capture.Id) ? "Focus · selected" : "Focus" : selected.Contains(capture.Id) ? "Selected" : "";
        card.State.Visibility = card.State.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(card.Frame, $"Screenshot {stamp.Number} of {stamp.Total}, {capture.Width} by {capture.Height}{(capture.Pinned ? ", pinned" : "")}{(selected.Contains(capture.Id) ? ", selected" : "")}. Space selects, Enter edits, Delete dismisses.");
        card.Controls.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        return card.Frame;
    }
    private CardVisual CreateCard(CaptureRecord capture)
    {
        CardBuildCount++;
        var size = controller.Settings.ThumbnailSize;
        var grid = new Grid { Width = size, Height = Math.Clamp(size * (double)capture.Height / Math.Max(1, capture.Width), 72, size * 0.65), ClipToBounds = true };
        grid.Clip = new RectangleGeometry(new Rect(0, 0, grid.Width, grid.Height), 6, 6);
        var available = true;
        try
        {
            var thumb = thumbnails.GetOrAdd((capture.FileName, size * 2), () => { var decoded = ImageService.Load(controller.Repository.PathFor(capture), size * 2, size * 2); ThumbnailDecodeCount++; return decoded; });
            grid.Children.Add(new Image { Source = thumb, Stretch = Stretch.Uniform });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidDataException or ArgumentException)
        { available = false; grid.Children.Add(Ui.Text("Image unavailable", 12)); }
        var border = new Border { Child = grid, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(2), BorderBrush = selected.Contains(capture.Id) ? (Brush)FindResource("Accent") : new SolidColorBrush(Color.FromArgb(90, 120, 136, 156)), Background = (Brush)FindResource("Surface"), Margin = new Thickness(3), Focusable = true, Tag = capture.Id };
        AutomationProperties.SetName(border, $"Screenshot {capture.Width} by {capture.Height}{(capture.Pinned ? ", pinned" : "")}. Click to edit; drag to attach.");
        var badge = Ui.Button($"{visible.IndexOf(capture) + 1}/{visible.Count}{(capture.Pinned ? " · pin" : "")}", "Focus this screenshot for keyboard actions without opening the editor", () => FocusCapture(capture.Id));
        badge.FontSize = 10; badge.Padding = new Thickness(6, 2, 6, 2); badge.HorizontalAlignment = HorizontalAlignment.Right; badge.VerticalAlignment = VerticalAlignment.Top; badge.Opacity = 0.9; grid.Children.Add(badge);
        var state = Ui.Text("", 10); state.HorizontalAlignment = HorizontalAlignment.Left; state.VerticalAlignment = VerticalAlignment.Top;
        state.Background = (Brush)FindResource("Surface"); state.IsHitTestVisible = false; grid.Children.Add(state);
        state.MaxWidth = size * 0.55;
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
        border.ContextMenu = Menu(capture);
        border.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (FindButton(e.OriginalSource as DependencyObject)) return;
            keyboardMode = false; down = e.GetPosition(this); pressed = capture; dragging = false;
            selection.Focus(capture.Id);
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                ToggleSelection(capture.Id);
                border.BorderBrush = (Brush)FindResource(selected.Contains(capture.Id) ? "Accent" : "Muted");
            }
            border.Focus(); border.CaptureMouse(); e.Handled = true;
        };
        border.GotKeyboardFocus += (_, _) => { selection.Focus(capture.Id); PaintStates(); };
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
        border.LostMouseCapture += (_, _) => { if (pressed == capture && !dragging) { pressed = null; collapseTimer.Start(); } };
        border.AllowDrop = true;
        border.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent("SnippyGrab.Reorder") ? DragDropEffects.Move : DragDropEffects.None; if (e.Effects == DragDropEffects.Move) { border.BorderBrush = (Brush)FindResource("Accent"); border.ToolTip = "Move screenshot to this position"; } e.Handled = true; };
        border.DragLeave += (_, _) => { border.BorderBrush = (Brush)FindResource(selected.Contains(capture.Id) ? "Accent" : "Muted"); border.ToolTip = null; };
        border.Drop += (_, e) =>
        {
            if (e.Data.GetData("SnippyGrab.Reorder") is string id && Guid.TryParse(id, out var guid)) controller.Reorder(guid, capture.Id);
            e.Handled = true;
        };
        return new(border, controls, state, available);
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
            ("Focus this screenshot (keyboard)", () => FocusCapture(record.Id)),
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
        // Leave ordinary Enter/Space behavior on toolbar buttons intact.
        if (FindButton(e.OriginalSource as DependencyObject) && e.Key is Key.Enter or Key.Space) return;
        if (HandleKey(e.Key, Keyboard.Modifiers)) e.Handled = true;
    }
    internal bool HandleKey(Key key, ModifierKeys modifiers)
    {
        if (selection.Current is null) return false;
        ShelfAction? action = key switch
        {
            Key.Delete when modifiers == ModifierKeys.None => ShelfAction.Dismiss,
            Key.Enter when modifiers == ModifierKeys.None => ShelfAction.Edit,
            Key.C when modifiers == ModifierKeys.Control => ShelfAction.Copy,
            Key.S when modifiers == ModifierKeys.Control => ShelfAction.Export,
            Key.P when modifiers == ModifierKeys.Control => ShelfAction.Pin,
            Key.H when modifiers == ModifierKeys.Control => ShelfAction.History,
            Key.OemComma when modifiers == ModifierKeys.Control => ShelfAction.Settings,
            _ => null
        };
        if (action is { } command) { keyboardMode = true; hideTimer.Stop(); collapseTimer.Stop(); Execute(command); PaintStates(); return true; }
        if (key == Key.Space && modifiers is ModifierKeys.None or ModifierKeys.Control) { keyboardMode = true; selection.Toggle(selection.Focused!.Value); PaintStates(); return true; }
        if (key == Key.Escape && modifiers == ModifierKeys.None) { selected.Clear(); keyboardMode = false; expanded = false; Keyboard.ClearFocus(); Rebuild(); ScheduleHide(); return true; }
        if (key is Key.Up or Key.Left or Key.Down or Key.Right or Key.Home or Key.End && modifiers is ModifierKeys.None or ModifierKeys.Control)
        {
            keyboardMode = true; hideTimer.Stop(); collapseTimer.Stop();
            var reverse = DockLayout.Reverse(controller.Settings.Orientation, controller.Settings.Corner);
            if (key == Key.Home) selection.Focus(visible[0].Id);
            else if (key == Key.End) selection.Focus(visible[^1].Id);
            else selection.Move((key is Key.Up or Key.Left ? -1 : 1) * (reverse ? -1 : 1));
            EnsureFocusedVisible(); if (IsKeyboardFocusWithin) FocusCurrentCard(); return true;
        }
        return false;
    }
    private void Execute(ShelfAction action)
    {
        var targets = selection.Targets(action);
        if (CommandSinkOverride is { } sink) { sink(action, targets); return; }
        if (targets.Count == 0) return;
        switch (action)
        {
            case ShelfAction.Copy: controller.Run(() => targets.Count == 1 ? controller.Copy(targets[0]) : controller.CopyFiles(targets)); break;
            case ShelfAction.Dismiss: controller.Dismiss(targets); break;
            case ShelfAction.Edit: controller.Edit(targets[0]); break;
            case ShelfAction.Export: controller.Save(targets[0]); break;
            case ShelfAction.Pin: controller.Pin(targets[0]); break;
            case ShelfAction.History: controller.ShowHistory(); break;
            case ShelfAction.Settings: controller.ShowSettings(); break;
        }
    }
    private void PaintStates()
    {
        foreach (var capture in visible.Where(c => shelf.Children.OfType<Border>().Any(b => b.Tag is Guid id && id == c.Id))) Card(capture);
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
