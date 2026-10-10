using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SnippyGrab.App;
using SnippyGrab.App.Services;
using SnippyGrab.App.Views;
using SnippyGrab.Core;

namespace SnippyGrab.IntegrationTests;

[CollectionDefinition("Application resources", DisableParallelization = true)]
public sealed class ApplicationResourcesCollection;

[Collection("Application resources")]
public sealed class UiRedesignTests
{
    private static void VerifyIconTextAlignment()
    {
        foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
            foreach (var scale in new[] { 1.0, 1.5, 2.25 })
            {
                Ui.Theme(theme, scale);
                using var menu = new System.Windows.Forms.ContextMenuStrip
                {
                    Renderer = new TrayRenderer(),
                    Font = new System.Drawing.Font("Segoe UI", (float)(10.5 * scale)),
                    ImageScalingSize = new System.Drawing.Size(18, 18)
                };
                foreach (var (label, icon, shortcut) in new[] { ("Region", "capture", "PrintScreen"), ("Window", "window", "Ctrl+Alt+PrintScreen"), ("Recent captures", "history", ""), ("Settings", "settings", ""), ("Launch at Windows login", "check", ""), ("Exit", "close", "") })
                    menu.Items.Add(new System.Windows.Forms.ToolStripMenuItem(label) { Image = TrayRenderer.Placeholder, Tag = icon, Padding = new System.Windows.Forms.Padding(6, 6, 8, 6), ShortcutKeyDisplayString = shortcut, Checked = icon == "check" });
                var textDraws = 0;
                menu.Renderer.RenderItemText += (_, e) =>
                {
                    textDraws++;
                    Assert.Equal(0, e.TextRectangle.Top); Assert.Equal(e.Item.Height, e.TextRectangle.Height);
                    Assert.True(e.TextFormat.HasFlag(System.Windows.Forms.TextFormatFlags.VerticalCenter));
                };
                menu.PerformLayout();
                using var bitmap = new System.Drawing.Bitmap(menu.Width, menu.Height);
                menu.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
                Assert.True(textDraws >= menu.Items.Count);
                var output = Environment.GetEnvironmentVariable("SNIPPYGRAB_ALIGNMENT_PREVIEWS");
                if (!string.IsNullOrEmpty(output)) { Directory.CreateDirectory(output); bitmap.Save(Path.Combine(output, $"tray-{theme}-{scale}.png")); }
                foreach (var label in new[] { "Copy image", "Screenshot shelf", "Long dialog heading" })
                {
                    var row = Ui.IconLabel("image", label);
                    row.Measure(new Size(600, 100)); row.Arrange(new Rect(row.DesiredSize)); row.UpdateLayout();
                    var icon = (FrameworkElement)row.Children[0]; var text = (FrameworkElement)row.Children[1];
                    var iconCenter = icon.TranslatePoint(new Point(0, icon.ActualHeight / 2), row).Y;
                    var textCenter = text.TranslatePoint(new Point(0, text.ActualHeight / 2), row).Y;
                    Assert.InRange(Math.Abs(iconCenter - textCenter), 0, 0.5);
                }
                var dialog = new DialogWindow("Delete selected screenshots", "Alignment fixture");
                try
                {
                    var heading = ((DockPanel)dialog.Content).Children.OfType<StackPanel>().Single();
                    heading.Measure(new Size(300, 200)); heading.Arrange(new Rect(heading.DesiredSize)); heading.UpdateLayout();
                    var icon = (FrameworkElement)heading.Children[0]; var title = (FrameworkElement)heading.Children[1];
                    Assert.InRange(Math.Abs(icon.TranslatePoint(new Point(0, icon.ActualHeight / 2), heading).Y - title.TranslatePoint(new Point(0, title.ActualHeight / 2), heading).Y), 0, 0.5);
                }
                finally { dialog.Close(); }
            }
        Ui.Theme(AppTheme.Dark, 1);
    }
    private static void VerifyUpdatesView(AppController controller)
    {
        using var updates = new UpdateService("0.1.0-alpha", new UpdateFixtureHandler());
        updates.CheckAsync().GetAwaiter().GetResult();
        Assert.Equal(UpdateState.Available, updates.State);
        foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
        {
            Ui.Theme(theme, 1);
            var window = new UpdatesWindow(controller, updates);
            window.ShowActivated = false; window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -30000; window.Top = -30000; window.Show();
            try
            {
                window.UpdateLayout();
                var root = (DockPanel)window.Content;
                Assert.Contains("Changelog fixture", root.Children.OfType<TextBox>().Single().Text);
                Assert.Contains("Update available", root.Children.OfType<StackPanel>().Single().Children.OfType<TextBlock>().ElementAt(2).Text);
                var output = Environment.GetEnvironmentVariable("SNIPPYGRAB_UPDATE_PREVIEWS");
                if (!string.IsNullOrEmpty(output))
                {
                    Directory.CreateDirectory(output);
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    var visual = new DrawingVisual();
                    using (var drawing = visual.RenderOpen()) { var bounds = new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight); drawing.DrawRectangle(Ui.Brush("Surface"), null, bounds); drawing.DrawRectangle(new VisualBrush(root), null, bounds); }
                    bitmap.Render(visual);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using var file = File.Create(Path.Combine(output, $"updates-{theme}.png")); encoder.Save(file);
                }
            }
            finally { window.Close(); }
            using var menu = new System.Windows.Forms.ContextMenuStrip { Renderer = new TrayRenderer(), Font = new System.Drawing.Font("Segoe UI", 10.5f) };
            foreach (var state in new[] { UpdateState.Current, UpdateState.Available, UpdateState.Failed })
                menu.Items.Add(new System.Windows.Forms.ToolStripMenuItem("Help & about · " + state) { Tag = state, Image = TrayRenderer.Placeholder, Padding = new System.Windows.Forms.Padding(6, 6, 8, 6) });
            menu.PerformLayout();
            using var rendered = new System.Drawing.Bitmap(menu.Width, menu.Height); menu.DrawToBitmap(rendered, new System.Drawing.Rectangle(0, 0, rendered.Width, rendered.Height));
            var previews = Environment.GetEnvironmentVariable("SNIPPYGRAB_UPDATE_PREVIEWS");
            if (!string.IsNullOrEmpty(previews)) rendered.Save(Path.Combine(previews, $"update-dots-{theme}.png"));
        }
        Ui.Theme(AppTheme.Dark, 1);
    }
    private sealed class UpdateFixtureHandler : System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(JsonSerializer.Serialize(new[] { new { tag_name = "v0.1.0-beta.1", draft = false, prerelease = true, html_url = "https://github.com/How-e/SnippyGrab/releases/tag/v0.1.0-beta.1", body = "Changelog fixture\n\nA quieter update experience.\n- Release status in Help & about\n- Verified updates with restart\n- Captures and settings retained", assets = new[] { new { name = "SnippyGrab-0.1.0-beta.1-win-x64.zip", state = "uploaded", digest = "sha256:" + new string('a', 64), browser_download_url = "https://github.com/How-e/SnippyGrab/releases/download/v0.1.0-beta.1/app.zip" } } } })) });
    }
    private sealed class TestApplication : Application
    {
        protected override void OnStartup(StartupEventArgs e) { }
    }
    [Fact]
    public void CategorizedDraftAndThemedViewsPreserveTheirBehavior()
    {
        ImageIntegrationTests.Sta(() =>
        {
            var app = new TestApplication { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/SnippyGrab;component/Views/Controls.xaml") });
            var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-ui-test-" + Guid.NewGuid().ToString("N"));
            new SettingsService(Path.Combine(root, "settings.json")).Save(new Settings { FirstRunComplete = true, Theme = AppTheme.Dark, RetentionHours = 13, DockMonitor = 99, DockMonitorIdentity = "disconnected-test-display", Animate = false });
            try
            {
                using var controller = new AppController(true, root, diagnostic: true);
                VerifyUpdatesView(controller);
                var original = JsonSerializer.Serialize(controller.Settings);
                var settings = new SettingsWindow(controller);
                try
                {
                    var editable = typeof(Settings).GetProperties().Select(p => p.Name).Except([nameof(Settings.SchemaVersion), nameof(Settings.FirstRunComplete)]).Order().ToArray();
                    Assert.Equal(editable, settings.EditableSettings.Order());
                    Assert.Equal(13, settings.BuildDraft().RetentionHours);
                    Assert.Equal("disconnected-test-display", settings.BuildDraft().DockMonitorIdentity);
                    settings.SelectCategory(2); ((TextBox)settings.InputFor(nameof(Settings.ThumbnailSize))).Text = "377";
                    settings.SelectCategory(3); ((CheckBox)settings.InputFor(nameof(Settings.AutoCopy))).IsChecked = false;
                    settings.SelectCategory(5); ((TextBox)settings.InputFor(nameof(Settings.StrokeSize))).Text = "invalid";
                    settings.SelectCategory(0);
                    Assert.Throws<InvalidDataException>(() => settings.BuildDraft());
                    Assert.Equal(5, settings.ActiveCategory);
                    ((TextBox)settings.InputFor(nameof(Settings.StrokeSize))).Text = "7";
                    settings.SelectCategory(1); var draft = settings.BuildDraft();
                    Assert.Equal(377, draft.ThumbnailSize); Assert.False(draft.AutoCopy); Assert.Equal(7, draft.StrokeSize);
                    ((TextBox)settings.InputFor(nameof(Settings.AnnotationColor))).Text = "";
                    Assert.Throws<InvalidDataException>(() => settings.BuildDraft()); Assert.Equal(5, settings.ActiveCategory);
                    ((TextBox)settings.InputFor(nameof(Settings.AnnotationColor))).Text = "#EF675E";
                    ((TextBox)settings.InputFor(nameof(Settings.DockOpacity))).Text = "NaN";
                    Assert.Throws<InvalidDataException>(() => settings.BuildDraft()); Assert.Equal(2, settings.ActiveCategory);
                    ((TextBox)settings.InputFor(nameof(Settings.DockOpacity))).Text = "0.96";
                    var export = ((DockPanel)settings.InputFor(nameof(Settings.SaveDirectory))).Children.OfType<TextBox>().Single();
                    var destination = export.Text; export.Text = controller.Repository.Root;
                    Assert.Throws<InvalidDataException>(() => settings.BuildDraft()); Assert.Equal(4, settings.ActiveCategory);
                    export.Text = destination;
                    Assert.Equal(original, JsonSerializer.Serialize(controller.Settings));
                    settings.SelectCategory(1);
                    foreach (var theme in Enum.GetValues<AppTheme>())
                    {
                        Ui.Theme(theme);
                        settings.Measure(new Size(960, 760)); settings.Arrange(new Rect(0, 0, 960, 760)); settings.UpdateLayout();
                        Assert.Equal(Ui.Brush("Surface"), settings.Background);
                        Assert.Equal(Ui.Brush("Raised"), ((TextBox)settings.InputFor(nameof(Settings.PrimaryHotkey))).Background);
                    }
                    foreach (var scale in new[] { 1.0, 1.5, 2.25 })
                    {
                        Ui.Theme(AppTheme.Dark, scale);
                        settings.Measure(new Size(520, 500)); settings.Arrange(new Rect(0, 0, 520, 500)); settings.UpdateLayout();
                        for (var page = 0; page < SettingsWindow.Categories.Length; page++) { settings.SelectCategory(page); settings.UpdateLayout(); }
                        Assert.Equal(377, settings.BuildDraft().ThumbnailSize);
                    }
                    Ui.Theme(AppTheme.Dark, 1, highContrastOverride: true);
                    Assert.Same(SystemColors.WindowBrush, Ui.Brush("Surface")); Assert.Same(SystemColors.HighlightTextBrush, Ui.Brush("SelectedInk"));
                    Assert.Same(SystemColors.HighlightTextBrush, Ui.Brush("SelectedIcon"));
                }
                finally { settings.Close(); }
                Assert.Equal(original, JsonSerializer.Serialize(controller.Settings)); // Cancel never saves the draft.
                Ui.Theme(AppTheme.Dark, 1);
                VerifyIconTextAlignment();
                var png = ImageService.Png(EditorDocumentTests.Fixture());
                for (var index = 0; index < 70; index++) controller.Repository.Add(png, 160, 100);
                var previousSessionOnly = controller.Settings.SessionOnly;
                controller.Settings.SessionOnly = true;
                Assert.Throws<IOException>(() => controller.ExitForUpdateAsync(() => throw new IOException("Update launcher fixture failure")).GetAwaiter().GetResult());
                Assert.False(controller.Exiting);
                Assert.Equal(70, controller.Repository.Captures.Count);
                Assert.All(controller.Repository.Captures, record => Assert.True(File.Exists(controller.Repository.PathFor(record))));
                controller.Settings.SessionOnly = previousSessionOnly;
                static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
                {
                    for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                    {
                        var child = VisualTreeHelper.GetChild(root, i);
                        if (child is T found) yield return found;
                        foreach (var nested in Children<T>(child)) yield return nested;
                    }
                }
                void LayoutWindow(Window window)
                {
                    window.ShowActivated = false; window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -30000; window.Top = -30000; window.Show();
                    window.Measure(new Size(720, 700)); window.Arrange(new Rect(0, 0, 720, 700)); window.UpdateLayout();
                }
                var combineInputs = controller.Repository.Captures.Take(2).Select(c => new CompositionInput(controller.Repository.PathFor(c), c.Width, c.Height)).ToArray();
                var combine = new CompositionWindow(combineInputs, null);
                try
                {
                    LayoutWindow(combine);
                    Children<Expander>(combine).Single().IsExpanded = true; combine.UpdateLayout();
                    var layout = Children<ComboBox>(combine).Single(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Composition layout");
                    var spacing = Children<TextBox>(combine).Single(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Spacing in pixels");
                    var create = Children<Button>(combine).Single(b => b.Content as string == "Create image & edit");
                    Assert.Equal(200, combine.Layout!.Height);
                    layout.SelectedIndex = 1; Assert.Equal(320, combine.Layout!.Width);
                    spacing.Text = "invalid"; Assert.Null(combine.Layout); Assert.False(create.IsEnabled);
                    spacing.Text = "8"; Assert.Equal(328, combine.Layout!.Width); Assert.True(create.IsEnabled);
                    Children<Button>(combine).Single(b => b.Content as string == "Move down").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(combineInputs[1], combine.Inputs[0]);
                    layout.SelectedIndex = 2; Assert.Equal(328, combine.Layout!.Width);
                }
                finally { combine.Close(); }
                using (var session = new ScrollSession(Path.Combine(root, "guided-scroll")))
                {
                    var first = EditorDocumentTests.Fixture(); session.Stage(first);
                    var scroll = new ScrollCaptureWindow(session, first, () => throw new InvalidOperationException("Capture is disabled in test."), (_, _) => throw new InvalidOperationException("Commit is disabled in test."));
                    try
                    {
                        LayoutWindow(scroll);
                        var buttons = Children<Button>(scroll).Where(b => b.Content is string).ToDictionary(b => (string)b.Content);
                        Assert.True(buttons["Capture next frame"].IsEnabled);
                        Assert.False(buttons["Keep this frame"].IsEnabled);
                        Assert.False(buttons["Discard this frame"].IsEnabled);
                        Assert.False(buttons["Undo last frame"].IsEnabled);
                        Assert.True(buttons["Create screenshot"].IsEnabled);
                    }
                    finally { scroll.Close(); }
                }
                using (var session = new ScrollSession(Path.Combine(root, "guided-scroll-pending")))
                {
                    var first = EditorDocumentTests.Fixture(); session.Stage(first); session.Stage(first);
                    var scroll = new ScrollCaptureWindow(session, first, () => throw new InvalidOperationException("Capture is disabled in test."), (_, _) => throw new InvalidOperationException("Commit is disabled in test."));
                    try
                    {
                        LayoutWindow(scroll);
                        var buttons = Children<Button>(scroll).Where(b => b.Content is string).ToDictionary(b => (string)b.Content);
                        Assert.False(buttons["Capture next frame"].IsEnabled); Assert.False(buttons["Create screenshot"].IsEnabled);
                        Assert.True(buttons["Keep this frame"].IsEnabled); Assert.True(buttons["Discard this frame"].IsEnabled);
                        buttons["Discard this frame"].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Assert.Null(session.Pending); Assert.Single(session.Frames);
                        Assert.True(buttons["Capture next frame"].IsEnabled); Assert.True(buttons["Create screenshot"].IsEnabled);
                    }
                    finally { scroll.Close(); }
                }
                var history = new HistoryWindow(controller);
                try
                {
                    var view = (FrameworkElement)history.Content; view.Measure(new Size(1080, 720)); view.Arrange(new Rect(0, 0, 1080, 720)); view.UpdateLayout();
                    Assert.True(VirtualizingPanel.GetIsVirtualizing(history.CaptureList)); Assert.InRange(history.CachedThumbnailCount, 1, 24);
                    history.CaptureList.SelectedIndex = 0; var id = controller.Repository.Captures[0].Id;
                    controller.Repository.Replace(controller.Repository.Captures[0], png, 160, 100);
                    var previewTask = history.PreviewWork;
                    var previewFrame = new System.Windows.Threading.DispatcherFrame();
                    _ = previewTask.ContinueWith(_ => history.Dispatcher.BeginInvoke(new Action(() => previewFrame.Continue = false)));
                    System.Windows.Threading.Dispatcher.PushFrame(previewFrame); previewTask.GetAwaiter().GetResult();
                    Assert.Single(history.CaptureList.SelectedItems); Assert.NotNull(history.PreviewImage.Source);
                    var selectedCapture = controller.Repository.Captures[0]; selectedCapture.Saved = true; controller.Repository.Persist();
                    Assert.Same(previewTask, history.PreviewWork); Assert.NotNull(history.PreviewImage.Source);
                    Assert.Equal(id, controller.Repository.Captures[0].Id);
                }
                finally { history.Close(); }
                var editor = new EditorWindow(controller, controller.Repository.Captures[0]);
                var exportRecord = controller.Repository.Captures[0]; exportRecord.ExportPath = Path.Combine(root, "previous.jpg");
                Assert.EndsWith(".png", ExportWindow.SuggestedFileName(exportRecord, ExportFormat.Png));
                var exportWindow = new ExportWindow(exportRecord, root, controller.Repository.Root, null);
                try
                {
                    var form = (StackPanel)((ScrollViewer)exportWindow.Content).Content;
                    var format = form.Children.OfType<ComboBox>().Single(); var quality = form.Children.OfType<Slider>().Single();
                    Assert.Equal(0, format.SelectedIndex); Assert.Equal(90, quality.Value); Assert.False(quality.IsEnabled);
                    format.SelectedIndex = 1; Assert.True(quality.IsEnabled); quality.Value = 75;
                    Assert.Contains(form.Children.OfType<TextBlock>(), text => text.Text == "Lossy quality: 75");
                    format.SelectedIndex = 2; Assert.False(quality.IsEnabled);
                    format.SelectedIndex = 3; Assert.True(quality.IsEnabled);
                    Assert.EndsWith(".webp", ExportWindow.SuggestedFileName(exportRecord, ExportFormat.WebpLossless));
                    Assert.Null(exportWindow.Request);
                    exportWindow.Measure(new Size(560, 360)); exportWindow.Arrange(new Rect(0, 0, 560, 360)); exportWindow.UpdateLayout();
                }
                finally { exportWindow.Close(); }
                var operation = new OperationWindow("Export image") { ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000 };
                operation.Show();
                try
                {
                    var form = (StackPanel)((ScrollViewer)operation.Content).Content;
                    var cancel = form.Children.OfType<Button>().Single();
                    cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.True(operation.Cancellation.IsCancellationRequested);
                    operation.Complete("Export cancelled before file commit."); operation.Report("Late worker progress");
                    Assert.Equal("Export cancelled before file commit.", form.Children.OfType<TextBlock>().Last().Text);
                }
                finally { operation.Complete("Completed"); operation.Close(); }
                foreach (var tool in Enum.GetValues<EditTool>()) { editor.ToolPicker.SelectedValue = tool; Assert.Equal(tool, editor.ToolPicker.SelectedValue); }
                var closed = false; editor.Closed += (_, _) => closed = true;
                editor.Close();
                var frame = new System.Windows.Threading.DispatcherFrame();
                editor.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => frame.Continue = false));
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                Assert.True(closed); // Native Close must not reenter WPF's Closing event.
                var dock = controller.Dock; dock.Refresh(); dock.SetExpanded(true);
                var records = controller.Repository.Captures.Take(3).ToArray();
                dock.ToggleSelection(records[0].Id); dock.ToggleSelection(records[1].Id);
                Assert.Equal(2, dock.SelectionCount);
                var card = ((StackPanel)((Border)dock.Content).Child).Children.OfType<Border>().Single(b => b.Tag is Guid id && id == records[0].Id);
                var delete = Children<Button>(card).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Delete from shelf (Delete)");
                delete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(records[0].Dismissed); Assert.True(records[1].Dismissed); Assert.False(records[2].Dismissed);
                var importPath = Path.Combine(root, "import.png"); File.WriteAllBytes(importPath, png);
                var priorCount = controller.Repository.Captures.Count;
                RunUiTask(async () => { await controller.ImportAsync(importPath); return true; });
                Assert.Equal(priorCount + 1, controller.Repository.Captures.Count);
                var opening = controller.Repository.Captures[0];
                var opened = RunUiTask(async () =>
                {
                    var first = controller.OpenEditorAsync(opening, show: false);
                    Assert.Null(await controller.OpenEditorAsync(opening, show: false));
                    var replacement = EditorDocumentTests.Fixture(200, 120);
                    controller.Repository.Replace(opening, ImageService.Png(replacement), 200, 120);
                    var result = await first; Assert.NotNull(result); Assert.Equal(200, result.BaseImage.PixelWidth);
                    Assert.Same(result, await controller.OpenEditorAsync(opening, show: false));
                    Assert.True(await result.RequestCloseAsync()); return true;
                });
                Assert.True(opened);
                priorCount = controller.Repository.Captures.Count;
                RunUiTask(async () => { var pending = controller.ImportAsync(importPath); controller.Dispose(); await pending; return true; });
                Assert.Equal(priorCount, controller.Repository.Captures.Count);
                controller.Dispose(); controller.Dock.Close();
            }
            finally
            {
                app.Shutdown();
                Assert.StartsWith(Path.Combine(Path.GetTempPath(), "SnippyGrab-ui-test-"), root, StringComparison.OrdinalIgnoreCase);
                Directory.Delete(root, true);
            }
            return true;
        });
    }
    private static T RunUiTask<T>(Func<Task<T>> action)
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var task = dispatcher.InvokeAsync(action).Task.Unwrap();
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timeout = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        timeout.Tick += (_, _) => frame.Continue = false; timeout.Start();
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
        System.Windows.Threading.Dispatcher.PushFrame(frame); timeout.Stop();
        Assert.True(task.IsCompleted, "UI operation did not complete within 15 seconds."); return task.GetAwaiter().GetResult();
    }
}
