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
                var png = ImageService.Png(EditorDocumentTests.Fixture());
                for (var index = 0; index < 70; index++) controller.Repository.Add(png, 160, 100);
                var history = new HistoryWindow(controller);
                try
                {
                    var view = (FrameworkElement)history.Content; view.Measure(new Size(1080, 720)); view.Arrange(new Rect(0, 0, 1080, 720)); view.UpdateLayout();
                    Assert.True(VirtualizingPanel.GetIsVirtualizing(history.CaptureList)); Assert.InRange(history.CachedThumbnailCount, 1, 24);
                    history.CaptureList.SelectedIndex = 0; var id = controller.Repository.Captures[0].Id;
                    controller.Repository.Replace(controller.Repository.Captures[0], png, 160, 100);
                    Assert.Single(history.CaptureList.SelectedItems); Assert.NotNull(history.PreviewImage.Source);
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
                foreach (var tool in Enum.GetValues<EditTool>()) { editor.ToolPicker.SelectedValue = tool; Assert.Equal(tool, editor.ToolPicker.SelectedValue); }
                var closed = false; editor.Closed += (_, _) => closed = true;
                editor.Close();
                var frame = new System.Windows.Threading.DispatcherFrame();
                editor.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => frame.Continue = false));
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                Assert.True(closed); // Native Close must not reenter WPF's Closing event.
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
}
