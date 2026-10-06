using Microsoft.Win32;

namespace SnippyGrab.App.Views;

internal sealed class HistoryWindow : Window
{
    private readonly AppController controller;
    private readonly ListBox list = new() { SelectionMode = SelectionMode.Extended, DisplayMemberPath = "Label", Background = Brushes.Transparent, Foreground = (Brush)Application.Current.FindResource("Ink"), BorderThickness = new Thickness(0) };
    private readonly Image preview = new() { Stretch = Stretch.Uniform, MaxHeight = 180, Margin = new Thickness(6) };
    private sealed record Row(CaptureRecord Capture)
    {
        public string Label => $"{Capture.CreatedUtc.LocalDateTime:g}    {Capture.Width} × {Capture.Height}    {(Capture.Pinned ? "PIN" : "")}{(Capture.Edited ? "  edited" : "")}{(Capture.Saved ? "  saved" : "")}";
    }
    public HistoryWindow(AppController controller)
    {
        Ui.StyleWindow(this);
        this.controller = controller; Title = "SnippyGrab · Recent captures"; Width = 650; Height = 570; MinWidth = 430; MinHeight = 350;
        var root = new DockPanel { Margin = new Thickness(18) }; Content = root;
        var header = Ui.Text("Recent captures", 22); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        if (controller.Repository.CleanupBlocked)
        {
            var recovery = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
            var guidance = Ui.Text("History could not be read. New pins and transfers are recovered across restarts. Unknown old captures are pinned to keep them safe. Cleanup stays disabled until you confirm recovery.", 13);
            guidance.TextWrapping = TextWrapping.Wrap; recovery.Children.Add(guidance);
            recovery.Children.Add(Ui.Button("Confirm recovered history…", "Preserve the unreadable original and enable cleanup with unknown captures pinned", () => controller.Try(() =>
            {
                if (MessageBox.Show(this, "Keep this recovered history? The unreadable original will be preserved in the cache. Unknown old captures remain pinned; review them before unpinning. Cleanup will be enabled after recovery succeeds.", "Confirm history recovery", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
                controller.Repository.ConfirmHistoryRecovery(); recovery.Visibility = Visibility.Collapsed; Refresh(); controller.Dock.Refresh();
            })));
            DockPanel.SetDock(recovery, Dock.Top); root.Children.Add(recovery);
        }
        var toolbar = new WrapPanel();
        toolbar.Children.Add(Ui.Button("To shelf", "Restore selected captures to shelf", () => controller.Try(() => { controller.Repository.Restore(Selected(), DateTimeOffset.UtcNow); controller.Dock.Reveal(); })));
        toolbar.Children.Add(Ui.Button("Copy", "Copy image or selected files", () => controller.Run(async () => { var items = Selected(); if (items.Count == 1) await controller.Copy(items[0]); else if (items.Count > 1) await controller.CopyFiles(items); })));
        toolbar.Children.Add(Ui.Button("Edit", "Edit selected capture", () => { if (Selected().FirstOrDefault() is { } c) controller.Edit(c); }));
        toolbar.Children.Add(Ui.Button("Pin", "Toggle pin on selected captures", () => { foreach (var c in Selected()) controller.Pin(c); Refresh(); }));
        toolbar.Children.Add(Ui.Button("Import…", "Import a local PNG, JPEG or BMP", () => { var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp", Multiselect = true }; if (dialog.ShowDialog(this) == true) { foreach (var path in dialog.FileNames) controller.Import(path); Refresh(); } }));
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        DockPanel.SetDock(preview, Dock.Bottom); root.Children.Add(preview); root.Children.Add(list);
        VirtualizingPanel.SetIsVirtualizing(list, true); VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        list.SelectionChanged += (_, _) => controller.Try(() => { var c = Selected().FirstOrDefault(); if (c is not null) controller.Repository.ResolveDimensions(c); preview.Source = c is null ? null : Services.ImageService.Load(controller.Repository.PathFor(c), 600); });
        list.MouseDoubleClick += (_, _) => { if (Selected().FirstOrDefault() is { } c) controller.Edit(c); };
        list.PreviewMouseMove += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed && Selected().Count > 0 && (Keyboard.Modifiers & ModifierKeys.Alt) != 0) controller.Try(() => controller.DragDrop.Drag(list, Selected())); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); if (e.Key == Key.Delete) { controller.Dismiss(Selected()); Refresh(); } };
        controller.Repository.RevisionChanged += RevisionChanged;
        Closed += (_, _) => controller.Repository.RevisionChanged -= RevisionChanged;
        Refresh();
    }
    private List<CaptureRecord> Selected() => list.SelectedItems.Cast<Row>().Select(r => r.Capture).ToList();
    private void RevisionChanged(CaptureRecord record) => controller.Try(Refresh);
    private void Refresh()
    {
        var selected = Selected().Select(c => c.Id).ToHashSet();
        list.ItemsSource = controller.Repository.Captures.OrderByDescending(c => c.CreatedUtc).Select(c => new Row(c)).ToList();
        foreach (var row in list.Items.Cast<Row>().Where(r => selected.Contains(r.Capture.Id))) list.SelectedItems.Add(row);
    }
}
