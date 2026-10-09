using System.Windows.Automation;
using System.Windows.Markup;
using Microsoft.Win32;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal sealed class HistoryWindow : Window
{
    internal ListBox CaptureList => list;
    internal Image PreviewImage => preview;
    internal int CachedThumbnailCount => thumbnails.Count;
    internal double PreviewWidth => Math.Max(600, previewPane.ActualWidth);
    private readonly AppController controller;
    private readonly ListBox list = new() { SelectionMode = SelectionMode.Extended };
    private readonly BoundedCache<(string File, int Pixels), BitmapSource> thumbnails = new(24);
    private readonly Image preview = new() { Stretch = Stretch.Uniform, Margin = new Thickness(12) };
    private readonly TextBlock pageStatus = Ui.Text("", 12, true);
    private readonly TextBlock count = Ui.Text("", 14, true);
    private readonly TextBlock selectionStatus = Ui.Text("Select a capture", 14, true);
    private readonly TextBlock metadata = Ui.Text("", 12, true);
    private readonly TextBlock empty = Ui.Text("Your captures will appear here.\nPress Print Screen or capture from the tray.", 16, true);
    private readonly TextBlock previewHint = Ui.Text("Select a capture to preview it.", 14, true);
    private readonly List<Button> selectionActions = [];
    private readonly Button copy;
    private readonly Button previous;
    private readonly Button next;
    private readonly Grid split = new();
    private readonly Border previewPane;
    private int page;
    private bool refreshing;
    private sealed record Row(CaptureRecord Capture, Func<CaptureRecord, BitmapSource?> Load)
    {
        public string Title => "Screenshot · " + Capture.CreatedUtc.LocalDateTime.ToString("t");
        public string Details => Capture.DimensionsPending ? "Dimensions pending" : $"{Capture.Width} × {Capture.Height}";
        public string State => string.Join(" · ", new[] { Capture.Pinned ? "Pinned" : "", Capture.Edited ? "Edited" : "", Capture.Saved ? "Exported" : "" }.Where(s => s.Length > 0));
        public BitmapSource? Thumbnail => Load(Capture);
    }
    public HistoryWindow(AppController controller)
    {
        Ui.StyleWindow(this); this.controller = controller; Title = "SnippyGrab · Recent captures"; Width = 1100; Height = 760; MinWidth = 560; MinHeight = 460;
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        var headerActions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        headerActions.Children.Add(Ui.ActionButton("Import…", "export", "Import local PNG, JPEG or BMP images", Import));
        headerActions.Children.Add(Ui.ActionButton("Capture region", "capture", "Capture a new region", () => controller.Run(() => controller.Capture(CaptureMode.Region)), "PrimaryButton"));
        DockPanel.SetDock(headerActions, Dock.Right); header.Children.Add(headerActions);
        header.SizeChanged += (_, _) => { headerActions.MaxWidth = Math.Max(240, header.ActualWidth); DockPanel.SetDock(headerActions, header.ActualWidth < 640 * Ui.TextScale ? Dock.Top : Dock.Right); };
        var title = new StackPanel(); title.Children.Add(Ui.Heading("Recent captures")); title.Children.Add(count); header.Children.Add(title); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        if (controller.Repository.CleanupBlocked)
        {
            var recovery = new StackPanel(); recovery.Children.Add(Ui.Heading("History needs recovery", 16)); recovery.Children.Add(Ui.Text("Unknown old captures are pinned to keep them safe. Cleanup stays disabled until you confirm recovery.", 12, true));
            recovery.Children.Add(Ui.Button("Confirm recovered history…", "Preserve original history and enable cleanup", () => controller.Try(() =>
            {
                if (!DialogWindow.Confirm("Keep recovered history?", "The unreadable original will be preserved in the cache.\n\nUnknown old captures remain pinned; review them before unpinning. Cleanup will be enabled after recovery succeeds.", "Keep recovered history", this)) return;
                controller.Repository.ConfirmHistoryRecovery(); ((FrameworkElement)recovery.Parent).Visibility = Visibility.Collapsed; Refresh(); controller.Dock.Refresh();
            })));
            var group = Ui.Group(recovery); DockPanel.SetDock(group, Dock.Top); root.Children.Add(group);
        }
        var toolbar = new WrapPanel { VerticalAlignment = VerticalAlignment.Center }; selectionStatus.Margin = new Thickness(4, 8, 18, 8); toolbar.Children.Add(selectionStatus);
        Button Action(string label, string icon, string hint, Action action) { var b = Ui.ActionButton(label, icon, hint, action, "QuietButton"); selectionActions.Add(b); toolbar.Children.Add(b); return b; }
        Action("To shelf", "image", "Restore selected captures to shelf", () => { controller.Repository.Restore(Selected(), DateTimeOffset.UtcNow); controller.Dock.Reveal(); });
        copy = Action("Copy image", "copy", "Copy image or selected PNG files", () => controller.Run(async () => { var selected = Selected(); if (selected.Count == 1) await controller.Copy(selected[0]); else if (selected.Count > 1) await controller.CopyFiles(selected); }));
        Action("Pin", "pin", "Toggle pins on selected captures", () => { foreach (var capture in Selected()) controller.Pin(capture); Refresh(); });
        Action("More", "more", "More selected capture actions", () =>
        {
            var menu = new ContextMenu(); menu.Items.Add(Ui.Menu("Edit", "edit", () => { if (Selected().FirstOrDefault() is { } c) controller.Edit(c); }, "Enter")); menu.Items.Add(Ui.Menu("Dismiss", "close", () => { controller.Dismiss(Selected()); Refresh(); }, "Delete")); menu.Items.Add(Ui.Menu("Export PNG…", "export", () => { if (Selected().FirstOrDefault() is { } c) controller.Save(c); }, "Ctrl+S")); menu.Items.Add(Ui.Menu("Copy OCR text", "ocr", () => { if (Selected().FirstOrDefault() is { } c) controller.Run(() => controller.Ocr(c)); })); menu.PlacementTarget = toolbar; menu.IsOpen = true;
        });
        var selectionBar = Ui.Group(toolbar, new Thickness(8)); DockPanel.SetDock(selectionBar, Dock.Top); root.Children.Add(selectionBar);
        var footer = new DockPanel { Margin = new Thickness(0, 16, 0, 0) };
        var paging = new WrapPanel(); previous = Ui.ActionButton("Previous", "left", "Previous history page", () => { page = Math.Max(0, page - 1); Refresh(); }); next = Ui.ActionButton("Next", "right", "Next history page", () => { page++; Refresh(); });
        pageStatus.VerticalAlignment = VerticalAlignment.Center; pageStatus.Margin = new Thickness(12, 0, 12, 0); paging.Children.Add(previous); paging.Children.Add(pageStatus); paging.Children.Add(next); DockPanel.SetDock(paging, Dock.Left); footer.Children.Add(paging);
        footer.SizeChanged += (_, _) => { paging.MaxWidth = Math.Max(240, footer.ActualWidth); DockPanel.SetDock(paging, footer.ActualWidth < 640 * Ui.TextScale ? Dock.Top : Dock.Left); };
        var notice = Ui.Text(controller.Settings.HistoryEnabled ? "Dismiss hides from the shelf. Clear temporary removes eligible files." : "Unpinned history is disabled. Pins and this session are shown.", 12, true); notice.Margin = new Thickness(20, 4, 0, 0); notice.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(notice); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.42, GridUnitType.Star) }); split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.58, GridUnitType.Star) }); root.Children.Add(split);
        var listPane = new Grid { Margin = new Thickness(0, 0, 18, 0) }; listPane.Children.Add(list); empty.Margin = new Thickness(18, 38, 18, 0); listPane.Children.Add(empty); split.Children.Add(listPane);
        list.ItemTemplate = (DataTemplate)XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="130"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Border Background="{DynamicResource Canvas}" CornerRadius="5" Margin="0,0,14,0"><Image Source="{Binding Thumbnail}" Height="82" Stretch="Uniform"/></Border><StackPanel Grid.Column="1" VerticalAlignment="Center"><TextBlock Text="{Binding Title}" FontWeight="SemiBold" Margin="0,0,0,7"/><TextBlock Text="{Binding Details}" Foreground="{Binding Foreground, RelativeSource={RelativeSource AncestorType=ListBoxItem}}"/><TextBlock Text="{Binding State}" Foreground="{Binding Foreground, RelativeSource={RelativeSource AncestorType=ListBoxItem}}" Margin="0,5,0,0"/></StackPanel></Grid></DataTemplate>
            """);
        AutomationProperties.SetName(list, "Recent captures. Ctrl-click selects several; Enter edits; Delete dismisses; Alt-drag attaches PNG files.");
        VirtualizingPanel.SetIsVirtualizing(list, true); VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling); ScrollViewer.SetCanContentScroll(list, true);
        var detail = new DockPanel { Margin = new Thickness(22, 0, 0, 0) };
        var detailActions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        void Detail(string label, string icon, Action action) { var b = Ui.ActionButton(label, icon, label + " selected capture", action); selectionActions.Add(b); detailActions.Children.Add(b); }
        Detail("Edit", "edit", () => { if (Selected().FirstOrDefault() is { } c) controller.Edit(c); }); Detail("Copy image", "copy", () => { if (Selected().FirstOrDefault() is { } c) controller.Run(() => controller.Copy(c)); }); Detail("Export PNG…", "export", () => { if (Selected().FirstOrDefault() is { } c) controller.Save(c, this); });
        DockPanel.SetDock(detailActions, Dock.Bottom); detail.Children.Add(detailActions); metadata.Margin = new Thickness(0, 12, 0, 0); DockPanel.SetDock(metadata, Dock.Bottom); detail.Children.Add(metadata);
        var previewContainer = new Grid(); previewContainer.SetResourceReference(Panel.BackgroundProperty, "Canvas"); previewContainer.Children.Add(preview); previewHint.VerticalAlignment = VerticalAlignment.Center; previewHint.HorizontalAlignment = HorizontalAlignment.Center; previewContainer.Children.Add(previewHint); detail.Children.Add(previewContainer);
        previewPane = new Border { Child = detail, BorderThickness = new Thickness(1, 0, 0, 0) }; previewPane.SetResourceReference(Border.BorderBrushProperty, "Border"); Grid.SetColumn(previewPane, 1); split.Children.Add(previewPane);
        SizeChanged += (_, _) => Responsive(); Ui.ThemeChanged += Responsive; Closed += (_, _) => Ui.ThemeChanged -= Responsive;
        list.SelectionChanged += (_, _) => { if (!refreshing) controller.Try(RefreshPreview); };
        list.MouseDoubleClick += (_, _) => { if (Selected().FirstOrDefault() is { } c) controller.Edit(c); };
        list.PreviewMouseMove += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed && Selected().Count > 0 && (Keyboard.Modifiers & ModifierKeys.Alt) != 0) controller.Try(() => controller.DragDrop.Drag(list, Selected())); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None) { controller.Dismiss(Selected()); Refresh(); e.Handled = true; } if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && Selected().FirstOrDefault() is { } c) { controller.Edit(c); e.Handled = true; } };
        controller.Repository.Changed += RepositoryChanged; controller.SettingsChanged += RepositoryChanged;
        DpiChanged += (_, e) => { if (ReferenceEquals(e.Source, this)) Dispatcher.BeginInvoke(new Action(() => { thumbnails.RemoveWhere(_ => true); if (IsLoaded) controller.Try(Refresh); })); };
        Closed += (_, _) => { controller.Repository.Changed -= RepositoryChanged; controller.SettingsChanged -= RepositoryChanged; thumbnails.RemoveWhere(_ => true); preview.Source = null; }; Refresh();
    }
    private void Responsive()
    {
        var compact = ActualWidth < 800 || Ui.TextScale > 1.5;
        previewPane.Visibility = compact ? Visibility.Collapsed : Visibility.Visible; split.ColumnDefinitions[0].Width = new GridLength(compact ? 1 : 0.42, GridUnitType.Star); split.ColumnDefinitions[1].Width = new GridLength(compact ? 0 : 0.58, GridUnitType.Star);
    }
    private void Import() { var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp", Multiselect = true }; if (dialog.ShowDialog(this) == true) { foreach (var path in dialog.FileNames) controller.Import(path); Refresh(); } }
    private List<CaptureRecord> Selected() => list.SelectedItems.Cast<Row>().Select(r => r.Capture).ToList();
    private BitmapSource? LoadThumbnail(CaptureRecord capture)
    {
        try { var pixels = Math.Max(120, (int)Math.Ceiling(120 * VisualTreeHelper.GetDpi(this).DpiScaleX)); return thumbnails.GetOrAdd((capture.FileName, pixels), () => ImageService.Load(controller.Repository.PathFor(capture), pixels, pixels)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidDataException or ArgumentException) { return null; }
    }
    private void RefreshPreview()
    {
        preview.Source = null; metadata.Text = "";
        var selected = Selected(); var capture = selected.FirstOrDefault(); foreach (var button in selectionActions) button.IsEnabled = selected.Count > 0;
        copy.Content = Ui.IconLabel("copy", selected.Count > 1 ? "Copy files" : "Copy image"); selectionStatus.Text = selected.Count == 0 ? "Select a capture" : $"{selected.Count} selected"; previewHint.Visibility = capture is null ? Visibility.Visible : Visibility.Collapsed;
        if (capture is not null) controller.Repository.ResolveDimensions(capture);
        preview.Source = capture is null ? null : ImageService.Load(controller.Repository.PathFor(capture), ImageService.PreviewPixels((int)Math.Ceiling(PreviewWidth * VisualTreeHelper.GetDpi(this).DpiScaleX), controller.Settings.PreviewQuality));
        metadata.Text = capture is null ? "" : $"{capture.Width} × {capture.Height}   ·   PNG   ·   {capture.CreatedUtc.LocalDateTime:g}"; RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.HighQuality);
    }
    private void RepositoryChanged() => controller.Try(Refresh);
    private void Refresh()
    {
        var selected = Selected().Select(c => c.Id).ToHashSet(); refreshing = true;
        try
        {
            page = Math.Min(page, Math.Max(0, (controller.Repository.Captures.Count - 1) / HistoryPage.Size));
            var files = controller.Repository.Captures.Select(c => c.FileName).ToHashSet(); thumbnails.RemoveWhere(key => !files.Contains(key.File));
            list.ItemsSource = HistoryPage.Read(controller.Repository.Captures, page).Select(c => new Row(c, LoadThumbnail)).ToArray();
            pageStatus.Text = $"Page {page + 1}"; count.Text = $"{controller.Repository.Captures.Count} captures"; previous.IsEnabled = page > 0; next.IsEnabled = (page + 1) * HistoryPage.Size < controller.Repository.Captures.Count;
            foreach (var row in list.Items.Cast<Row>().Where(r => selected.Contains(r.Capture.Id))) list.SelectedItems.Add(row);
            empty.Visibility = list.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { refreshing = false; }
        RefreshPreview();
    }
}
