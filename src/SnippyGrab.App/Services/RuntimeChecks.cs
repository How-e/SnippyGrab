using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Interop;
using SnippyGrab.App.Views;

namespace SnippyGrab.App.Services;

internal static class RuntimeChecks
{
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index); if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    public static async Task CheckOcrCorpus(string destination)
    {
        using var service = new OcrService();
        var cases = new[] { ("Build failed\nError CS1002: semicolon expected", "CS1002"), ("Traceback (most recent call last):\nValueError: invalid literal", "ValueError"), ("Application error\nAccess denied. Please retry.", "denied") };
        byte[]? last = null;
        foreach (var (text, expected) in cases)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 1200, 500));
                dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), 32, Brushes.Black, 1), new Point(30, 70));
            }
            var image = new RenderTargetBitmap(1200, 500, 96, 96, PixelFormats.Pbgra32); image.Render(visual); image.Freeze();
            last = ImageService.Png(image);
            Assert((await service.ReadAsync(last)).Contains(expected, StringComparison.OrdinalIgnoreCase), "Packaged full-image OCR corpus");
            Assert((await service.ReadAsync(ImageService.Png(ImageService.Crop(image, new(20, 50, 1160, 250))))).Contains(expected, StringComparison.OrdinalIgnoreCase), "Packaged area OCR corpus");
        }
        var malformedRejected = false;
        try { await service.ReadAsync(new byte[30]); } catch (InvalidDataException) { malformedRejected = true; }
        Assert(malformedRejected, "Malformed OCR input rejected");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var cancelled = false;
        try { await service.ReadAsync(last!, cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
        Assert(cancelled, "OCR cancellation preserved");
        AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(new { Result = "PASS", Corpus = 6, MalformedInputRejected = true, Cancellation = true, Scope = "Synthetic full/area native OCR corpus, malformed input and cancellation; no desktop capture or clipboard writes." }));
    }
    private sealed class PendingOcr : IOcrService
    {
        public readonly TaskCompletionSource<string> Completion = new();
        public Task<string> ReadAsync(byte[] png, CancellationToken cancellation = default) => Completion.Task;
    }
    public static async Task CheckReliability(string destination)
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-reliability-" + Guid.NewGuid().ToString("N"));
        var foreground = Native.GetForegroundWindow();
        new SettingsService(Path.Combine(root, "settings.json")).Save(new Settings { FirstRunComplete = true, Animate = false, AutoCopy = false });
        try
        {
            using var controller = new AppController(true, root, diagnostic: true);
            var startupCommand = StartupService.Command;
            var diagnosticSettings = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(controller.Settings))!;
            diagnosticSettings.LaunchOnStartup = true;
            controller.ApplySettings(diagnosticSettings);
            Assert(!controller.Settings.LaunchOnStartup && StartupService.Command == startupCommand, "Diagnostic settings cannot change Windows startup registration");
            controller.ConfigureHotkeys(false);
            Assert(controller.Hotkeys.Paused, "Diagnostic hotkeys remain paused after settings/pause restoration");
            diagnosticSettings = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(controller.Settings))!;
            diagnosticSettings.CachePath = Path.Combine(root, "other-cache");
            var cacheRejected = false;
            try { controller.ApplySettings(diagnosticSettings); } catch (InvalidDataException) { cacheRejected = true; }
            Assert(cacheRejected && controller.Settings.CachePath.Length == 0 && !Directory.Exists(diagnosticSettings.CachePath), "Diagnostic cache changes cannot escape the isolated root");
            var image = SyntheticCode(320, 160); var png = ImageService.Png(image);
            var record = controller.Repository.Add(png, 320, 160);
            var history = new HistoryWindow(controller) { ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000 };
            var pin = new PinWindow(controller, record) { ShowActivated = false, Topmost = false, Left = -30000, Top = -30000 };
            try
            {
                history.Show(); pin.Show(); await Task.Delay(30);
                var list = history.CaptureList; list.SelectedIndex = 0;
                controller.Repository.Add(png, 320, 160); Assert(list.Items.Count == 2 && list.SelectedItems.Count == 1, "Open history refresh preserves selection after capture");
                controller.Repository.Replace(record, ImageService.Png(SyntheticCode(640, 240)), 640, 240);
                await Task.WhenAll(history.PreviewWork, pin.PreviewWork);
                var historyImage = history.PreviewImage;
                var pinImage = pin.PreviewImage;
                Assert(((BitmapSource)historyImage.Source).PixelWidth == 640 && ((BitmapSource)pinImage.Source).PixelWidth == 640, $"History and detached pin refresh committed pixels (history={((BitmapSource)historyImage.Source).PixelWidth}, pin={((BitmapSource)pinImage.Source).PixelWidth})");
                var large = ImageService.Png(SyntheticCode(2240, 800));
                foreach (var quality in Enum.GetValues<PreviewQuality>())
                {
                    controller.Settings.PreviewQuality = quality;
                    controller.Repository.Replace(record, large, 2240, 800);
                    await Task.WhenAll(history.PreviewWork, pin.PreviewWork);
                    var expectedHistory = quality == PreviewQuality.Original ? 2240 : ImageService.PreviewPixels((int)Math.Ceiling(history.PreviewWidth * VisualTreeHelper.GetDpi(history).DpiScaleX), quality);
                    var expectedPin = quality == PreviewQuality.Original ? 2240 : ImageService.PreviewPixels(800, quality);
                    Assert(((BitmapSource)historyImage.Source).PixelWidth == expectedHistory && ((BitmapSource)pinImage.Source).PixelWidth == expectedPin, "Configured quality refreshes open history and pin previews");
                    Assert(File.ReadAllBytes(controller.Repository.PathFor(record)).SequenceEqual(large), "Preview quality preserves original PNG bytes");
                }
                controller.Settings.PreviewQuality = PreviewQuality.Sharp;
                // Deliberately constrain the requested size so this regression also
                // exercises monitor-style limits on larger local desktops.
                pin.MaxWidth = 1000;
                pin.Width = 1900; pin.Height = 900; pin.UpdateLayout();
                // Windows can constrain the realized HWND to the runner's monitor.
                // Verify detail for that physical width, rather than the requested DIP width.
                var resizedWidth = (int)Math.Ceiling(Math.Max(800, pin.ActualWidth * VisualTreeHelper.GetDpi(pin).DpiScaleX));
                var expectedResizedPreview = Math.Min(2240, ImageService.PreviewPixels(resizedWidth, PreviewQuality.Sharp));
                var resizeDeadline = Stopwatch.StartNew();
                while (((BitmapSource)pinImage.Source).PixelWidth != expectedResizedPreview && resizeDeadline.Elapsed < TimeSpan.FromSeconds(2)) await Task.Delay(50);
                Assert(expectedResizedPreview > ImageService.PreviewPixels(800, PreviewQuality.Sharp) && ((BitmapSource)pinImage.Source).PixelWidth == expectedResizedPreview,
                    $"Resizing a pin decodes detail beyond the old fixed 800-pixel cap (physical width={resizedWidth}, expected={expectedResizedPreview}, actual={((BitmapSource)pinImage.Source).PixelWidth})");
                controller.Repository.SetPinned(record, false); await Task.WhenAll(history.PreviewWork, pin.PreviewWork); controller.Repository.Cleanup(DateTimeOffset.UtcNow, 1, true);
                Assert(File.Exists(controller.Repository.PathFor(record)), "Unpinned open view protects current source");
                pin.Close(); await Task.WhenAll(history.PreviewWork, pin.DrainPreviewAsync()); controller.Repository.Cleanup(DateTimeOffset.UtcNow, 1, true); Assert(list.Items.Count == 0, "Pin close releases source and history removes cleaned rows");

                var writes = new List<DataObject>(); controller.Clipboard = new ClipboardService(data => writes.Add(data), _ => Task.CompletedTask);
                var pending = new PendingOcr(); controller.OcrService = pending;
                var oldOcr = controller.OcrText(image); await controller.CopyText("newer synthetic copy"); pending.Completion.SetResult("stale synthetic OCR");
                Assert(await oldOcr == "OCR cancelled." && writes.Count == 1, "New copy supersedes pending OCR before clipboard publication");
                pending = new PendingOcr(); controller.OcrService = pending;
                using var lifetime = new CancellationTokenSource(); oldOcr = controller.OcrText(image, lifetime.Token); lifetime.Cancel(); pending.Completion.SetResult("closed editor OCR");
                Assert(await oldOcr == "OCR cancelled." && writes.Count == 1, "Closed editor lifetime prevents clipboard publication");
                var editedRecord = controller.Repository.Add(png, 320, 160);
                var editor = new EditorWindow(controller, editedRecord) { ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000 };
                editor.Show();
                var work = editor.StartDocumentEdit(new(EditTool.Blur, new Point(20, 20), new Point(300, 140), Colors.Red, 3, 24, "", []));
                var close = editor.RequestCloseAsync();
                await work; Assert(await close && editedRecord.Edited && writes.Count == 2, "Close during worker effect waits and applies/copies final document");
                await CheckFailedEditorClose(controller, png);
                controller.Failure(new IOException("private-path-and-OCR-sentinel"));
                Assert(!controller.LastOperationDetails.Contains("sentinel", StringComparison.Ordinal) && controller.LastOperationDetails.Contains("retry", StringComparison.OrdinalIgnoreCase), "Failure details are actionable without private exception payload");
                Assert(!File.ReadAllText(Path.Combine(root, "diagnostics.log")).Contains("sentinel", StringComparison.Ordinal), "Diagnostics contain category/type instead of private exception payload");
                var previousNotice = controller.LastOperationDetails;
                controller.Failure(new OperationCanceledException("private-path-and-OCR-sentinel"));
                Assert(controller.LastOperationDetails == previousNotice, "Cancellation does not replace the useful failure notice");
                await CheckCapturePersistenceFailures(root, image, png);
                Assert(Native.GetForegroundWindow() == foreground, "Reliability probe preserves foreground");
            }
            finally { pin.Close(); history.Close(); await Task.WhenAll(history.DrainPreviewAsync(), pin.DrainPreviewAsync()); controller.Dock.Close(); }
            AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(new { Result = "PASS", Scope = "Offscreen synthetic history/pin revision and lease lifecycle, injected OCR/clipboard; no pointer movement, real capture, OS clipboard write or startup registration change." }));
        }
        finally
        {
            var absolute = Path.GetFullPath(root);
            if (!absolute.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(absolute).StartsWith("SnippyGrab-reliability-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected reliability probe path.");
            Directory.Delete(absolute, true);
        }
    }
    private static async Task CheckFailedEditorClose(AppController controller, byte[] png)
    {
        var record = controller.Repository.Add(png, 320, 160);
        var otherRecord = controller.Repository.Add(png, 320, 160);
        var editor = new EditorWindow(controller, record) { ShowActivated = false, Left = -30000, Top = -30000 };
        var other = new EditorWindow(controller, otherRecord) { ShowActivated = false, Left = -30000, Top = -30000 };
        var writes = 0;
        controller.Clipboard = new ClipboardService(_ => writes++, _ => Task.CompletedTask);
        editor.Show(); other.Show();
        try
        {
            var original = record.FileName;
            await editor.StartDocumentEdit(new(EditTool.Redact, new Point(20, 20), new Point(100, 100), Colors.Black, 3, 24, "", []));
            using (var locked = new FileStream(Path.Combine(controller.Repository.Root, "history.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert(!await editor.RequestCloseAsync() && editor.IsVisible, "Failed metadata apply retains actual WPF editor");
                Assert(record.FileName == original && !record.Edited && writes == 0, "Failed close preserves original revision and does not announce a copy");
                Assert(await other.RequestCloseAsync(), "Separate clean editor can close during failed apply");
            }
            controller.Clipboard = new ClipboardService(_ => throw new COMException("Injected clipboard contention"), _ => Task.CompletedTask);
            Assert(!await editor.RequestCloseAsync() && editor.IsVisible && record.Edited, "Exhausted clipboard retries retain saved edits and the editor");
            var applied = record.FileName;
            controller.Clipboard = new ClipboardService(_ => writes++, _ => Task.CompletedTask);
            Assert(await editor.RequestCloseAsync() && record.FileName == applied && writes == 1, "Retry closes without duplicating the applied revision");
        }
        finally
        {
            foreach (var window in new[] { editor, other }.Where(w => w.IsVisible))
                Descendants<Button>((DependencyObject)window.Content).Single(b => Equals(b.Content, "Discard changes")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
    }
    private static async Task CheckCapturePersistenceFailures(string parent, BitmapSource image, byte[] png)
    {
        foreach (var boundary in new[] { "png", "metadata" })
        {
            var root = Path.Combine(parent, "storage-" + boundary);
            new SettingsService(Path.Combine(root, "settings.json")).Save(new Settings { FirstRunComplete = true, Animate = false, AutoCopy = true });
            var fail = false;
            using var controller = new AppController(true, root, diagnostic: true, storageWriter: (path, bytes) =>
            {
                if (fail && (boundary == "png" ? path.EndsWith(".png", StringComparison.Ordinal) : path.EndsWith("history.json", StringComparison.Ordinal))) throw new IOException("injected storage failure");
                AtomicFile.Write(path, bytes);
            });
            var copies = 0; var refreshes = 0;
            controller.Clipboard = new ClipboardService(data => { Assert(data.GetDataPresent(DataFormats.Bitmap) && data.GetDataPresent("PNG"), "Fallback retains image and PNG clipboard formats"); copies++; }, _ => Task.CompletedTask);
            fail = true;
            await controller.PreserveCaptureAsync(image, png, "synthetic", () => refreshes++);
            Assert(copies == 1, "Capture reaches clipboard despite PNG or metadata storage failure");
            if (boundary == "png") Assert(controller.Repository.Captures.Count == 0 && refreshes == 0, "Failed PNG creates no misleading shelf record");
            else
            {
                Assert(controller.Repository.Captures.Count == 1 && refreshes == 1 && controller.Repository.PersistencePending, "Metadata failure preserves shelf pixels with cleanup paused");
                Assert(controller.Repository.Cleanup(DateTimeOffset.UtcNow, 1, true) == 0, "Pending history cannot authorize cleanup");
            }
            fail = false; controller.Repository.Persist();
            Assert(!controller.Repository.PersistencePending, "History retry clears persistence warning state");
            await controller.PreserveCaptureAsync(image, png, "synthetic", () => refreshes++);
            Assert(copies == 2 && refreshes > 0, "Capture can be retried after storage recovers");
            controller.Dispose(); controller.Dock.Close();
        }
    }
    public static async Task CheckEditorLayout(string destination)
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-editor-check-" + Guid.NewGuid().ToString("N"));
        var foreground = Native.GetForegroundWindow();
        new SettingsService(Path.Combine(root, "settings.json")).Save(new Settings { FirstRunComplete = true, Animate = false, Theme = AppTheme.Dark });
        try
        {
            using var controller = new AppController(true, root, diagnostic: true);
            var image = SyntheticCode(720, 360); var record = controller.Repository.Add(ImageService.Png(image), 720, 360);
            CaptureExport.Write(controller.Repository, record, Path.Combine(root, "exports", "synthetic.png"));
            var export = new ExportWindow(record, root, controller.Repository.Root, null) { ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000 };
            try
            {
                export.Show(); await Task.Delay(50); export.UpdateLayout();
                var content = (FrameworkElement)export.Content;
                var png = new RenderTargetBitmap((int)export.Width, (int)export.Height, 96, 96, PixelFormats.Pbgra32); png.Render(content);
                File.WriteAllBytes(destination + ".export.png", ImageService.Png(png));
                Assert(Descendants<ComboBox>(content).Single().SelectedIndex == 0, "Export defaults to PNG");
                foreach (var scale in new[] { 1.0, 1.5, 2.25 })
                {
                    Ui.Theme(AppTheme.Dark, scale); export.UpdateLayout();
                    Assert(Descendants<Slider>(content).Single().Value == 90, "JPEG default quality stays 90");
                    Assert(((ScrollViewer)content).VerticalScrollBarVisibility == ScrollBarVisibility.Auto, "Export options scroll at larger text scales");
                }
            }
            finally { export.Close(); Ui.Theme(AppTheme.Dark, 1); }
            // These isolated windows use synthetic captures and never invoke a file picker.
            var second = controller.Repository.Add(ImageService.Png(image), 720, 360);
            var ordered = TransferPayload.Ordered(controller.Repository, [record, second]);
            using (var scrolling = new ScrollSession(Path.Combine(root, "scroll-sessions")))
            {
                scrolling.Stage(image);
                var windows = new Window[] { new ExportWindow(record, root, controller.Repository.Root, null, ordered), new CompositionWindow(ordered.Select(c => new CompositionInput(controller.Repository.PathFor(c), c.Width, c.Height)).ToArray(), null), new ScrollCaptureWindow(scrolling, image, () => throw new InvalidOperationException("Diagnostic capture is disabled."), (_, _) => throw new InvalidOperationException("Diagnostic commit is disabled.")) };
                for (var i = 0; i < windows.Length; i++)
                {
                    var window = windows[i]; window.ShowActivated = false; window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -30000; window.Top = -30000;
                    try
                    {
                        window.Show(); await Task.Delay(50); window.UpdateLayout();
                        foreach (var scale in new[] { 1.0, 1.5, 2.25 })
                        {
                            Ui.Theme(AppTheme.Dark, scale); window.UpdateLayout();
                            var content = (FrameworkElement)window.Content;
                            Assert(content is ScrollViewer { VerticalScrollBarVisibility: ScrollBarVisibility.Auto } || Descendants<ScrollViewer>(content).Any(s => s.VerticalScrollBarVisibility == ScrollBarVisibility.Auto), "Improvement options remain scrollable at larger text scales");
                            foreach (var input in Descendants<Control>(content).Where(c => c is TextBox or ComboBox or Slider))
                                Assert(!string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(input)), "Improvement input exposes accessible name");
                            Snapshot(window, destination + $".improvement-{i}-text{(int)(scale * 100)}.png");
                        }
                    }
                    finally { window.Close(); Ui.Theme(AppTheme.Dark, 1); }
                }
            }
            var raw = new byte[128 * 64 * 4]; new Random(42).NextBytes(raw);
            var alpha = BitmapSource.Create(128, 64, 96, 96, PixelFormats.Bgra32, null, raw, 128 * 4); alpha.Freeze();
            File.WriteAllBytes(destination + ".expected-bgra", raw);
            File.WriteAllBytes(destination + ".lossless.webp", WebpService.Encode(alpha, true, 90));
            File.WriteAllBytes(destination + ".lossy20.webp", WebpService.Encode(alpha, false, 20));
            File.WriteAllBytes(destination + ".lossy100.webp", WebpService.Encode(alpha, false, 100));
            var editor = new EditorWindow(controller, record) { ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000 };
            try
            {
                editor.Show(); await Task.Delay(50);
                foreach (var textScale in new[] { 1.0, 1.5, 2.25 })
                    foreach (var width in new[] { 660, 1000 })
                    {
                        Ui.Theme(AppTheme.Dark, textScale);
                        editor.ToolPicker.SelectedValue = EditTool.OcrArea;
                        editor.Width = width; editor.UpdateLayout(); await Task.Delay(25);
                        var rootPanel = (DockPanel)editor.Content;
                        var buttons = Descendants<Button>(rootPanel).ToArray();
                        string Id(DependencyObject element) => System.Windows.Automation.AutomationProperties.GetAutomationId(element);
                        Assert(buttons.Any(b => Id(b) == "Apply + copy") && buttons.Any(b => Id(b) == "Export image…"), "Explicit apply/export actions");
                        Assert(editor.ExportFolderAction.IsEnabled, "Successful export exposes folder action");
                        foreach (var input in Descendants<Control>(rootPanel).Where(c => c is TextBox or ComboBox))
                            Assert(!string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(input)), "Editor input exposes an accessible name");
                        Assert(buttons.Where(b => b.IsVisible).All(b => b.ActualWidth >= 30 && b.ActualHeight >= 30), "Editor actions have minimum 30 DIP targets");
                        foreach (var control in buttons.Where(b => b.IsVisible && b.Tag is not EditTool))
                        {
                            var bounds = control.TransformToAncestor(rootPanel).TransformBounds(new Rect(control.RenderSize));
                            Assert(bounds.Left >= -1 && bounds.Right <= rootPanel.ActualWidth + 1 && bounds.Bottom <= rootPanel.ActualHeight, $"Every editor command stays reachable at {width} DIP and {textScale:P0} text");
                        }
                        Assert(buttons.Count(b => b.Tag is EditTool) == Enum.GetValues<EditTool>().Length, "All tools remain in the scrollable rail");
                        foreach (var tool in Enum.GetValues<EditTool>()) { editor.ToolPicker.SelectedValue = tool; editor.UpdateLayout(); }
                        Snapshot(editor, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination))!, $"editor-export-{width}-text{(int)(textScale * 100)}.png"));
                    }
                Assert(Native.GetForegroundWindow() == foreground, "Editor layout probe preserves focus");
            }
            finally { await editor.RequestCloseAsync(); }
            controller.Dispose(); controller.Dock.Close();
            AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(new { Result = "PASS", Widths = new[] { 660, 1000 }, TextScales = new[] { 1.0, 1.5, 2.25 }, Scope = "Offscreen synthetic editor labels/action/tool bounds; no clipboard writes, file dialogs, folder launch or real annotation gestures." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            var absolute = Path.GetFullPath(root);
            if (!absolute.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(absolute).StartsWith("SnippyGrab-editor-check-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected editor probe path.");
            Directory.Delete(absolute, true);
        }
    }

    public static async Task CheckDockLayout(string destination)
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-dock-check-" + Guid.NewGuid().ToString("N"));
        var results = new List<object>();
        var foreground = Native.GetForegroundWindow(); Native.GetCursorPos(out var pointer);
        new SettingsService(Path.Combine(root, "settings.json")).Save(new Settings { FirstRunComplete = true, Animate = false, AutoCopy = false, AutoHideSeconds = 0 });
        try
        {
            using var controller = new AppController(true, root, diagnostic: true);
            var dock = controller.Dock;
            dock.SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(dock).Handle).AddHook((nint h, int msg, nint w, nint l, ref bool handled) =>
            {
                if (msg == 0x0046)
                {
                    var pos = Marshal.PtrToStructure<Native.WINDOWPOS>(l);
                    var placement = DockLayout.Place(new(-30000, -30000, 10000, 10000), pos.Width, pos.Height, controller.Settings.Corner);
                    pos.X = placement.X; pos.Y = placement.Y; pos.Flags &= ~0x0002u;
                    Marshal.StructureToPtr(pos, l, false);
                }
                return 0;
            });
            var png = ImageService.Png(SyntheticCode(720, 360));
            foreach (var count in new[] { 1, 3, 5, 20 })
            {
                controller.Settings.DockOpacity = count == 5 ? 0.25 : 0.96;
                while (controller.Repository.Captures.Count < count)
                {
                    var n = controller.Repository.Captures.Count;
                    var width = n % 3 == 0 ? 720 : n % 3 == 1 ? 360 : 720;
                    var height = n % 3 == 0 ? 360 : n % 3 == 1 ? 720 : 120;
                    controller.Repository.Add(ImageService.Png(SyntheticCode(width, height)), width, height);
                }
                foreach (var corner in Enum.GetValues<DockCorner>())
                    foreach (var orientation in Enum.GetValues<DockOrientation>())
                    {
                        controller.Settings.Corner = corner; controller.Settings.Orientation = orientation;
                        dock.ResetPreviewCachesForCheck();
                        dock.SetExpanded(false); dock.Reveal(); await Task.Delay(35); dock.UpdateLayout();
                        var panel = (StackPanel)((Border)dock.Content).Child;
                        var id = controller.Repository.Captures[0].Id;
                        Border Primary() => panel.Children.OfType<Border>().Single(b => b.Tag is Guid guid && guid == id);
                        var before = Primary().PointToScreen(new Point(Primary().ActualWidth / 2, Primary().ActualHeight / 2));
                        var buildsBefore = dock.CardBuildCount; var decodesBefore = dock.ThumbnailDecodeCount;
                        dock.SetExpanded(true); await Task.Delay(35); dock.UpdateLayout();
                        var after = Primary().PointToScreen(new Point(Primary().ActualWidth / 2, Primary().ActualHeight / 2));
                        Assert(Math.Abs(before.X - after.X) <= 1 && Math.Abs(before.Y - after.Y) <= 1, "Primary card anchor survives expansion");
                        var samples = new List<Point>();
                        for (var cycle = 0; cycle < 3; cycle++)
                        {
                            dock.SetExpanded(false); await Task.Delay(20);
                            dock.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
                            for (var sample = 0; sample < 4; sample++)
                            {
                                await Task.Delay(20); dock.UpdateLayout();
                                var center = Primary().PointToScreen(new Point(Primary().ActualWidth / 2, Primary().ActualHeight / 2));
                                Assert(Math.Abs(center.X - before.X) <= 1 && Math.Abs(center.Y - before.Y) <= 1, "Repeated cold/warm hover samples keep primary anchor stable");
                                samples.Add(center);
                            }
                        }
                        Native.GetWindowRect(new WindowInteropHelper(dock).Handle, out var rect);
                        Assert(((SolidColorBrush)((Border)dock.Content).Background).Color.A > 0 && panel.Background == Brushes.Transparent, "Continuous nonzero-alpha hover surface");
                        Assert(dock.InputHitTest(new Point(1, 1)) is not null, "Hover route includes window padding");
                        var route = new RenderTargetBitmap(2, 2, 96, 96, PixelFormats.Pbgra32); route.Render(dock);
                        var pixel = new byte[4]; route.CopyPixels(new Int32Rect(1, 1, 1, 1), pixel, 4, 0);
                        Assert(pixel[3] > 0, "Native layered-window hover padding is not alpha-zero");
                        foreach (var card in panel.Children.OfType<Border>().Where(b => b.Tag is Guid))
                        {
                            var point = card.PointToScreen(new Point(card.ActualWidth / 2, card.ActualHeight / 2));
                            Assert(point.X >= rect.Left && point.X < rect.Right && point.Y >= rect.Top && point.Y < rect.Bottom, "Every displayed card is inside hover HWND");
                            var local = card.TranslatePoint(new Point(card.ActualWidth / 2, card.ActualHeight / 2), dock);
                            Assert(dock.InputHitTest(local) is not null, "Card is reachable through the hover surface");
                        }
                        var rebuilds = dock.RebuildCount;
                        var warmBuilds = dock.CardBuildCount; var warmDecodes = dock.ThumbnailDecodeCount;
                        for (var i = 0; i < 5; i++) dock.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
                        Assert(dock.RebuildCount == rebuilds, "Repeated enter does not rebuild expanded cards");
                        dock.ToggleSelection(id);
                        dock.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
                        await Task.Delay(220);
                        Assert(!dock.Expanded && dock.SelectionCount == 1, "Pointer leave collapses without losing selection");
                        dock.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
                        Assert(dock.Expanded && dock.SelectionCount == 1, "Re-entry preserves expansion and selection");
                        Assert(dock.CardBuildCount == warmBuilds && dock.ThumbnailDecodeCount == warmDecodes, "Warm hover and selection reuse cards and decoded thumbnails");
                        Assert(dock.CachedCardCount <= 5 && dock.CachedThumbnailCount <= 12, "Bounded dock caches");
                        dock.ToggleSelection(id);
                        results.Add(new { count, corner, orientation, Animate = controller.Settings.Animate, controller.Settings.DockOpacity, ColdCachesReset = true, AnchorSamples = samples, PrimaryAnchorStable = true, WarmCycleCardBuilds = dock.CardBuildCount - buildsBefore, ThumbnailDecodes = dock.ThumbnailDecodeCount - decodesBefore });
                    }
            }
            var hoverBuilds = results.ToArray();
            for (var i = 0; i < 20; i++)
            {
                dock.Scroll(1); await Task.Delay(10);
                Assert(dock.CachedCardCount <= 5 && dock.CachedThumbnailCount <= 12, "Scrolling keeps caches bounded");
            }
            dock.Scroll(-20);
            var beforeEditDecode = dock.ThumbnailDecodeCount;
            controller.Repository.Replace(controller.Repository.Captures[0], png, 720, 360); dock.Refresh();
            Assert(dock.ThumbnailDecodeCount == beforeEditDecode + 1, "New revision invalidates the cached image");
            var actions = new List<(ShelfAction Action, Guid[] Ids)>();
            dock.CommandSinkOverride = (action, captures) => actions.Add((action, captures.Select(c => c.Id).ToArray()));
            var captures = controller.Repository.Captures;
            dock.ToggleSelection(captures[7].Id);
            dock.HandleKey(Key.C, ModifierKeys.Control); dock.HandleKey(Key.Delete, ModifierKeys.None);
            Assert(actions.All(a => a.Ids.SequenceEqual(new[] { captures[7].Id })), "Single nonprimary keyboard targets");
            dock.ToggleSelection(captures[2].Id); dock.HandleKey(Key.Home, ModifierKeys.None);
            actions.Clear(); dock.HandleKey(Key.C, ModifierKeys.Control); dock.HandleKey(Key.Delete, ModifierKeys.None);
            Assert(actions.All(a => a.Ids.SequenceEqual(new[] { captures[2].Id, captures[7].Id })), "Selected set wins even when focused primary is excluded");
            dock.HandleKey(Key.End, ModifierKeys.None);
            Assert(dock.FocusedCapture == captures[^1].Id, "Keyboard navigation reaches scrolling capture");
            actions.Clear(); dock.HandleKey(Key.Enter, ModifierKeys.None); dock.HandleKey(Key.S, ModifierKeys.Control); dock.HandleKey(Key.P, ModifierKeys.Control);
            Assert(actions.All(a => a.Ids.SequenceEqual(new[] { captures[^1].Id })), "Single-item edit/export/pin use focused capture");
            dock.HandleKey(Key.Space, ModifierKeys.None); Assert(dock.SelectionCount == 3, "Keyboard multiple selection survives scrolling");
            dock.HandleKey(Key.Escape, ModifierKeys.None); Assert(dock.SelectionCount == 0 && !dock.Expanded, "Escape clears selection and collapses");
            dock.HandleKey(Key.Home, ModifierKeys.None);
            actions.Clear();
            Assert(!dock.HandleKey(Key.Delete, ModifierKeys.Control) && !dock.HandleKey(Key.Enter, ModifierKeys.Alt) && !dock.HandleKey(Key.C, ModifierKeys.None) && actions.Count == 0, "Unassigned key modifiers cannot invoke unrelated actions");
            var focusedBeforeHover = dock.FocusedCapture;
            dock.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            Assert(dock.FocusedCapture == focusedBeforeHover && !dock.IsKeyboardFocusWithin, "Raised hover does not acquire keyboard focus");
            dock.CommandSinkOverride = null;
            foreach (var thumbnailSize in new[] { 120, 180, 210, 224, 400 })
            {
                controller.Settings.ThumbnailSize = thumbnailSize; dock.Refresh(); dock.SetExpanded(true); dock.UpdateLayout();
                foreach (var card in ((StackPanel)((Border)dock.Content).Child).Children.OfType<Border>().Where(b => b.Tag is Guid))
                {
                    var grid = (Grid)card.Child; var toolbar = Descendants<StackPanel>(grid).Single();
                    Assert(toolbar.ActualWidth <= grid.ActualWidth, "Compact action toolbar fits screenshot width");
                    Assert(toolbar.TranslatePoint(new Point(0, toolbar.ActualHeight), grid).Y <= grid.ActualHeight + 1, "Actions overlay the bottom of the image without a separate footer");
                    Assert(toolbar.Children.Count == (thumbnailSize < 210 ? 4 : 7), "Shelf keeps Delete on the main toolbar at every size");
                    Assert(toolbar.Children.OfType<Button>().Any(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Delete from shelf (Delete)"), "Delete is directly accessible");
                    Assert(!card.ContextMenu.Items.OfType<MenuItem>().Any(m => m.InputGestureText == "Delete"), "Delete has moved out of More");
                }
            }
            Snapshot(dock, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination))!, "dock-layout-synthetic.png"));
            AtomicFile.Write(destination + ".layout.json", JsonSerializer.SerializeToUtf8Bytes(new { Result = "LAYOUT_PASS_ENVIRONMENT_PENDING", Layouts = hoverBuilds, ScrolledItems = 20, RevisionInvalidation = true, KeyboardCommandRouting = true, CachedCards = dock.CachedCardCount, CachedThumbnails = dock.CachedThumbnailCount, Scope = "All structural assertions passed; final foreground/pointer preservation has not yet been checked. This checkpoint is not an overall probe PASS." }, new JsonSerializerOptions { WriteIndented = true }));
            Assert(Native.GetForegroundWindow() == foreground, "Dock probe preserves foreground");
            Native.GetCursorPos(out var afterPointer); Assert(afterPointer.X == pointer.X && afterPointer.Y == pointer.Y, "Dock probe never moves pointer");
            controller.Dispose(); dock.Close();
            AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(new { Result = "PASS", Layouts = hoverBuilds, ScrolledItems = 20, RevisionInvalidation = true, KeyboardCommandRouting = true, CachedCards = dock.CachedCardCount, CachedThumbnails = dock.CachedThumbnailCount, Scope = "Offscreen synthetic WPF/native layout, raised hover events and keyboard command routing with intercepted side effects; no actual pointer gestures, OS clipboard writes or external receivers." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            var absolute = Path.GetFullPath(root);
            if (!absolute.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(absolute).StartsWith("SnippyGrab-dock-check-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected dock probe path.");
            Directory.Delete(absolute, true);
        }
    }

    // Safe during normal desktop use: only synthetic, offscreen windows; no pointer/clipboard/capture activity.
    public static async Task CheckOverlayLayout(string destination)
    {
        var foreground = Native.GetForegroundWindow();
        var image = SyntheticCode(320, 180);
        var desktop = Native.Desktop;
        var results = new List<object>();
        foreach (var size in new[] { (desktop.Width, desktop.Height, -30000), (desktop.Width, desktop.Height, 30000), (6400, 1821, -30000), (3840, 4320, -30000) })
        {
            var bounds = new PixelRect(size.Item3, -30000, size.Item1, size.Item2);
            var overlay = new CaptureOverlay(image, bounds, false, activate: false) { Topmost = false };
            try
            {
                overlay.Show(); await Task.Delay(100); overlay.UpdateLayout();
                Native.GetWindowRect(new WindowInteropHelper(overlay).Handle, out var actual);
                var dpi = VisualTreeHelper.GetDpi(overlay);
                var view = (System.Windows.Controls.Viewbox)overlay.Content;
                var rendered = view.Child.TransformToAncestor(overlay).TransformBounds(new Rect(0, 0, bounds.Width, bounds.Height));
                Assert(actual.Pixels == bounds, "Full physical overlay HWND bounds");
                Assert(Math.Abs(rendered.X) < 0.01 && Math.Abs(rendered.Y) < 0.01 &&
                    Math.Abs(rendered.Width * dpi.DpiScaleX - bounds.Width) < 1 &&
                    Math.Abs(rendered.Height * dpi.DpiScaleY - bounds.Height) < 1, "Snapshot maps to every overlay pixel");
                Assert(Native.GetForegroundWindow() == foreground, "Offscreen layout probe preserves focus");
                results.Add(new { Requested = bounds, Actual = actual.Pixels, dpi.DpiScaleX, dpi.DpiScaleY, RenderedDipBounds = rendered });
            }
            finally { overlay.Close(); }
        }
        AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(new { Result = "PASS", Desktop = desktop, Layouts = results, Scope = "Offscreen synthetic layout only; no pointer movement, clipboard write or desktop capture." }, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static async Task Run(string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        var tests = new Dictionary<string, object>();
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-test-" + Guid.NewGuid().ToString("N"));
        var repository = new CaptureRepository(root);
        try
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 720, 360));
                dc.DrawText(new FormattedText("Build failed\nError CS1002: semicolon expected", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), 32, Brushes.Black, 1), new Point(30, 70));
                dc.DrawRectangle(Brushes.RoyalBlue, null, new Rect(30, 240, 120, 60));
            }
            var synthetic = new RenderTargetBitmap(720, 360, 96, 96, PixelFormats.Pbgra32); synthetic.Render(visual); synthetic.Freeze();
            var png = ImageService.Png(synthetic); var record = repository.Add(png, 720, 360);
            var copy = new DataObject(); copy.SetImage(synthetic); copy.SetData("PNG", new MemoryStream(png));
            Assert(copy.GetDataPresent(DataFormats.Bitmap) && copy.GetDataPresent("PNG"), "Clipboard image/PNG construction");
            tests["clipboard_formats"] = copy.GetFormats();
            var files = new DataObject(DataFormats.FileDrop, TransferPayload.Files(repository, [record]));
            Assert(files.GetData(DataFormats.FileDrop) is string[] { Length: 1 }, "OLE file payload"); tests["file_drop"] = "single and multi checked";
            var second = repository.Add(png, 720, 360);
            Assert(TransferPayload.Files(repository, [record, second]).Length == 2, "Multi-file payload");
            var marks = new[] { new Annotation(EditTool.Redact, new Point(30, 240), new Point(150, 300), Colors.Black, 3, 24, "", [], null) };
            var rendered = EditorWindow.Render(new(synthetic, marks));
            var pixel = new byte[4]; rendered.CopyPixels(new Int32Rect(60, 260, 1, 1), pixel, 4, 0);
            Assert(pixel[0] == 0 && pixel[1] == 0 && pixel[2] == 0 && pixel[3] == 255, "Opaque redaction");
            tests["redaction"] = "opaque exported pixels verified";
            var crop = ImageService.Crop(rendered, new(20, 20, 300, 200)); Assert(crop.PixelWidth == 300 && crop.PixelHeight == 200, "Crop dimensions");
            var blur = Effects.Apply(crop, false); var mosaic = Effects.Apply(crop, true); Assert(blur.PixelWidth == 300 && mosaic.PixelHeight == 200, "Effects dimensions");
            tests["editor_effects"] = "crop, blur, pixelation rendered";
            var timer = Stopwatch.StartNew();
            var text = await new OcrService().ReadAsync(png);
            Assert(text.Contains("CS1002", StringComparison.OrdinalIgnoreCase) && text.Contains("Build", StringComparison.OrdinalIgnoreCase), "Local OCR recognition");
            tests["ocr"] = new { RecognizedExpectedTokens = true, ElapsedMs = timer.ElapsedMilliseconds };
            var window = new Window { Title = "SnippyGrab synthetic runtime check", Width = 400, Height = 240, Background = Brushes.RoyalBlue, Content = new TextBlock { Text = "SYNTHETIC CHECK", Foreground = Brushes.White, FontSize = 22, Margin = new Thickness(30) }, WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = false };
            window.Show(); window.Topmost = true; window.UpdateLayout();
            var handle = new WindowInteropHelper(window).Handle;
            // Command runners may give WinExe an initially hidden startup window.
            Assert(Native.SetWindowPos(handle, -1, 0, 0, 0, 0, 0x53), "Expose synthetic test HWND without activating it");
            // Check the actual exposed pixels rather than assuming a fixed startup delay
            // guarantees that the compositor has rendered an unoccluded test window.
            var ready = Stopwatch.StartNew();
            Native.RECT rect = default;
            var center = new byte[4];
            var exposed = false;
            while (true)
            {
                await Task.Delay(100); Native.DwmFlush();
                Native.GetWindowRect(handle, out rect);
                var point = new Native.POINT { X = (rect.Left + rect.Right) / 2, Y = (rect.Top + rect.Bottom) / 2 };
                exposed = Native.GetAncestor(Native.WindowFromPoint(point), 2) == handle;
                var sample = ImageService.Capture(rect.Pixels, false);
                sample.CopyPixels(new Int32Rect(sample.PixelWidth / 2, sample.PixelHeight / 2, 1, 1), center, 4, 0);
                if (exposed && center[0] > 140 && center[2] < 100) break;
                Assert(ready.Elapsed < TimeSpan.FromSeconds(3), $"Exposed rendered blue test window within three seconds (exposed={exposed}, bounds={rect.Pixels}, BGR={center[0]},{center[1]},{center[2]}, style={Native.GetWindowLongPtr(handle, -16):X}, extended={Native.GetWindowLongPtr(handle, -20):X})");
            }
            timer.Restart(); var captured = ImageService.Capture(rect.Pixels, false); var bytes = ImageService.Png(captured); var captureTime = timer.ElapsedMilliseconds;
            Assert(captured.PixelWidth == rect.Pixels.Width && bytes.Length > 0, "Native physical capture");
            // Confirm pixels rather than inferring rendering from a HWND alone.
            captured.CopyPixels(new Int32Rect(captured.PixelWidth / 2, captured.PixelHeight / 2, 1, 1), center, 4, 0);
            Assert(center[0] > 140 && center[2] < 100, "Rendered blue window pixels");
            window.Close(); tests["native_capture"] = new { Bounds = rect.Pixels, EncodeIncludedMs = captureTime, BluePixelsVerified = true };
            using (var hotkeys = new HotkeyService())
            {
                var settings = new Settings { PrimaryHotkey = new(119, 6) }; hotkeys.Configure(settings);
                tests["hotkey_registration"] = new { Warnings = hotkeys.Warnings.ToArray(), Fallback = settings.PrimaryHotkey.ToString() };
            }
            tests["monitors"] = MonitorService.All().Select(m => new { m.Index, m.Bounds, m.Dpi });
            tests["overlay_coordinates"] = await CheckOverlay(synthetic);
            tests["ui"] = await CheckUi(root, destination, png);
            tests["ole_delivery"] = await CheckOle(repository, [record, second]);
            tests["result"] = "PASS";
            AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(tests, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            // This is an explicitly constructed test-only directory, never the user's cache.
            var fullRoot = Path.GetFullPath(root);
            if (!Path.GetFileName(fullRoot).StartsWith("SnippyGrab-test-", StringComparison.Ordinal) || !fullRoot.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected test cleanup path.");
            Directory.Delete(fullRoot, true);
        }
    }


    public static async Task Benchmark(string destination)
    {
        var root = Path.Combine(Path.GetTempPath(), "SnippyGrab-benchmark-" + Guid.NewGuid().ToString("N"));
        var measurements = new Dictionary<string, object>();
        try
        {
            new SettingsService(Path.Combine(root, "settings.json")).Save(new Settings { FirstRunComplete = true, Animate = false, Theme = AppTheme.Dark });
            using var controller = new AppController(true, root); controller.Hotkeys.Configure(controller.Settings, true);
            await Task.Delay(2000);
            var process = Process.GetCurrentProcess(); process.Refresh();
            measurements["fresh_tray_working_set_mb"] = Math.Round(process.WorkingSet64 / 1048576.0, 1);
            var cpu = process.TotalProcessorTime; await Task.Delay(5000); process.Refresh();
            measurements["fresh_tray_cpu_ms_over_5_seconds"] = (process.TotalProcessorTime - cpu).TotalMilliseconds;
            var jobs = new List<object>();
            foreach (var dimensions in new[] { (720, 360), (3840, 2160), (7680, 4320) })
            {
                jobs.Add(BenchmarkImage(controller.Repository, dimensions.Item1, dimensions.Item2));
                GC.Collect(); GC.WaitForPendingFinalizers();
            }
            measurements["synthetic_png_pipeline"] = jobs;
            controller.Repository.Cleanup(DateTimeOffset.UtcNow, 1, true);
            var png = ImageService.Png(SyntheticCode(720, 360));
            for (var i = 0; i < 20; i++) controller.Repository.Add(png, 720, 360);
            controller.Dock.Refresh(true); await Task.Delay(2000); GC.Collect(); GC.WaitForPendingFinalizers();
            await Task.Delay(1000); process.Refresh(); measurements["twenty_capture_shelf_working_set_mb"] = Math.Round(process.WorkingSet64 / 1048576.0, 1);
            cpu = process.TotalProcessorTime; await Task.Delay(5000); process.Refresh();
            measurements["twenty_capture_shelf_cpu_ms_over_5_seconds"] = (process.TotalProcessorTime - cpu).TotalMilliseconds;
            measurements["scope"] = "Fresh process. Synthetic code images. PNG encoding, durable cache write and thumbnail decode; excludes screen acquisition and OS clipboard transfer.";
            controller.Dispose(); controller.Dock.Close();
            AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(measurements, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            var absolute = Path.GetFullPath(root);
            if (!absolute.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(absolute).StartsWith("SnippyGrab-benchmark-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected benchmark path.");
            Directory.Delete(absolute, true);
        }
    }
    private static object BenchmarkImage(CaptureRepository repository, int width, int height)
    {
        var image = SyntheticCode(width, height); var watch = Stopwatch.StartNew();
        var png = ImageService.Png(image); var encoded = watch.ElapsedMilliseconds;
        var record = repository.Add(png, width, height); var stored = watch.ElapsedMilliseconds;
        var thumb = ImageService.Load(repository.PathFor(record), 448);
        return new { Width = width, Height = height, PngBytes = png.Length, EncodeMs = encoded, EncodeAndStorageMs = stored, EncodeStorageThumbnailMs = watch.ElapsedMilliseconds, ThumbnailWidth = thumb.PixelWidth };
    }
    internal static BitmapSource SyntheticCode(int width, int height)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(26, 31, 38)), null, new Rect(0, 0, width, height));
            var scale = width / 1920.0;
            for (var y = 20.0; y < height - 40; y += 36 * Math.Max(1, scale))
                dc.DrawText(new FormattedText("public async Task<Capture> SnipAsync() => await Capture.Region();  // synthetic benchmark", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), 20 * Math.Max(1, scale), Brushes.LightSteelBlue, 1), new Point(20, y));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }

    private static async Task<object> CheckOverlay(BitmapSource synthetic)
    {
        Native.GetCursorPos(out var original); var foreground = Native.GetForegroundWindow();
        var measurements = new List<object>();
        try
        {
            foreach (var monitor in MonitorService.All())
            {
                var overlay = new CaptureOverlay(synthetic, Native.Desktop, false);
                overlay.Show(); await Task.Delay(150);
                var x = monitor.Bounds.X + 100; var y = monitor.Bounds.Y + 100;
                Native.SetCursorPos(x, y); await Task.Delay(30);
                Native.mouse_event(0x0002, 0, 0, 0, 0); await Task.Delay(30);
                Native.SetCursorPos(x + 160, y + 120); await Task.Delay(30);
                Native.mouse_event(0x0004, 0, 0, 0, 0); await Task.Delay(30);
                var expected = new PixelRect(x, y, 160, 120);
                var actual = overlay.Selection;
                overlay.Close(); Assert(actual == expected, "Overlay physical selection on monitor " + monitor.Index);
                measurements.Add(new { monitor.Index, monitor.Dpi, Expected = expected, Actual = actual });
            }
        }
        finally { Native.SetCursorPos(original.X, original.Y); if (foreground != 0) Native.SetForegroundWindow(foreground); }
        return measurements;
    }
    private static async Task<object> CheckUi(string root, string destination, byte[] png)
    {
        var isolated = Path.Combine(root, "app");
        new SettingsService(Path.Combine(isolated, "settings.json")).Save(new Settings { FirstRunComplete = true, StartMinimized = true, Animate = false, Theme = AppTheme.Dark });
        using var controller = new AppController(true, isolated);
        controller.Hotkeys.Configure(controller.Settings, true);
        for (var i = 0; i < 20; i++) controller.Repository.Add(png, 720, 360);
        controller.Dock.Refresh(true); await Task.Delay(100);
        var output = Path.GetDirectoryName(Path.GetFullPath(destination))!;
        Snapshot(controller.Dock, Path.Combine(output, "dock.png"));
        var initialWidth = controller.Dock.ActualWidth; var initialHeight = controller.Dock.ActualHeight;
        Assert(controller.Dock.ActualWidth < 280 && controller.Dock.ActualHeight < 210, "Compact 20-capture shelf");
        var settings = new SettingsWindow(controller, true); settings.Show(); await Task.Delay(100); Snapshot(settings, Path.Combine(output, "settings.png")); settings.Close();
        var welcome = new WelcomeWindow(controller); welcome.Show(); await Task.Delay(100); Snapshot(welcome, Path.Combine(output, "welcome.png")); welcome.Close();
        var editor = new EditorWindow(controller, controller.Repository.Captures[0]); editor.Show(); await Task.Delay(100); Snapshot(editor, Path.Combine(output, "editor.png")); editor.Close();
        var history = new HistoryWindow(controller); history.Show(); await Task.Delay(100); Snapshot(history, Path.Combine(output, "history.png")); history.Close();
        var primary = await CheckPrimaryWorkflow(controller);
        await Task.Delay(2000);
        GC.Collect(); GC.WaitForPendingFinalizers(); await Task.Delay(2000);
        var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime; var before = process.WorkingSet64;
        await Task.Delay(5000); process.Refresh();
        var result = new { FixtureCaptures = 20, TotalCaptures = controller.Repository.Captures.Count, InitialShelfWidthDip = initialWidth, InitialShelfHeightDip = initialHeight, WorkingSetMb = Math.Round(process.WorkingSet64 / 1048576.0, 1), IdleCpuMsOver5Seconds = (process.TotalProcessorTime - cpu).TotalMilliseconds, WorkingSetDeltaMb = Math.Round((process.WorkingSet64 - before) / 1048576.0, 1), PrimaryWorkflow = primary, Screenshots = "synthetic dock, editor, settings and history" };
        controller.Dispose(); controller.Dock.Close(); return result;
    }


    private static async Task<object> CheckPrimaryWorkflow(AppController controller)
    {
        Native.GetCursorPos(out var pointer); var original = Native.GetForegroundWindow();
        var previousCount = controller.Repository.Captures.Count;
        controller.Settings.AutoCopy = false; // The harness must preserve the user's clipboard.
        controller.Hotkeys.Configure(controller.Settings);
        if (controller.Hotkeys.Warnings.Count > 0) return new { Status = "SKIPPED: hotkey conflict", Warnings = controller.Hotkeys.Warnings.ToArray() };
        var window = new Window { Title = "Synthetic Print Screen workflow", Width = 420, Height = 250, Background = Brushes.RoyalBlue, Content = new TextBlock { Text = "SYNTHETIC CAPTURE", Foreground = Brushes.White, Margin = new Thickness(20) }, WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = false };
        try
        {
            window.Show(); await Task.Delay(100); var hwnd = new WindowInteropHelper(window).Handle;
            Native.SetForegroundWindow(hwnd); Native.GetWindowRect(hwnd, out var bounds);
            Native.keybd_event(44, 0, 0, 0); Native.keybd_event(44, 0, 2, 0);
            await Task.Delay(250);
            Native.SetCursorPos(bounds.Left + 40, bounds.Top + 70); Native.mouse_event(0x0002, 0, 0, 0, 0); await Task.Delay(40);
            Native.SetCursorPos(bounds.Left + 200, bounds.Top + 170); await Task.Delay(40);
            Native.mouse_event(0x0004, 0, 0, 0, 0); await Task.Delay(250);
            Assert(controller.Repository.Captures.Count == previousCount + 1, "Print Screen through capture to cache/shelf");
            var record = controller.Repository.Captures[0];
            Assert(record.Width == 160 && record.Height == 100, "Primary workflow crop size");
            Assert(Native.GetForegroundWindow() == hwnd, "Focus restored after capture");
            Assert(controller.Dock.IsVisible && File.Exists(controller.Repository.PathFor(record)), "Capture availability");
            return new { Status = "PASS", PrintScreen = "native key event -> registered hotkey -> region overlay", ExactRegion = "160x100", ForegroundRestored = true, FileAndShelfAvailable = true, Clipboard = "disabled only in harness to preserve user data" };
        }
        finally { controller.Hotkeys.Configure(controller.Settings, true); window.Close(); Native.SetCursorPos(pointer.X, pointer.Y); if (original != 0) Native.SetForegroundWindow(original); }
    }

    private static async Task<object> CheckOle(CaptureRepository repository, CaptureRecord[] records)
    {
        var foreground = Native.GetForegroundWindow(); Native.GetCursorPos(out var previous);
        string[]? received = null;
        var target = new Window { Title = "Synthetic OLE receiver", Width = 340, Height = 180, AllowDrop = true, Background = Brushes.DarkSlateBlue, Content = new TextBlock { Text = "SYNTHETIC FILE RECEIVER", Foreground = Brushes.White }, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        target.DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
        target.Drop += (_, e) => { received = e.Data.GetData(DataFormats.FileDrop) as string[]; e.Handled = true; };
        try
        {
            target.Show(); await Task.Delay(100);
            Native.GetWindowRect(new WindowInteropHelper(target).Handle, out var box);
            Native.SetCursorPos(box.Left + 60, box.Top + 80); Native.mouse_event(0x0002, 0, 0, 0, 0);
            var release = Task.Run(async () =>
            {
                await Task.Delay(350); Native.SetCursorPos(box.Left + 100, box.Top + 100);
                await Task.Delay(200); Native.mouse_event(0x0004, 0, 0, 0, 0);
            });
            new DragDropService(repository).Drag(target, records);
            await release;
            Assert(received is { Length: 2 } && received.All(File.Exists), "Native OLE multi-file delivery");
            return new { FilesReceived = received!.Length, SourceFilesValidAfterDrop = true, Receiver = "local synthetic WPF target, not Codex" };
        }
        finally { Native.mouse_event(0x0004, 0, 0, 0, 0); target.Close(); Native.SetCursorPos(previous.X, previous.Y); if (foreground != 0) Native.SetForegroundWindow(foreground); }
    }

    private static void Snapshot(Window window, string path)
    {
        window.UpdateLayout();
        var content = (FrameworkElement)window.Content;
        var width = Math.Max(1, (int)Math.Ceiling(content.ActualWidth)); var height = Math.Max(1, (int)Math.Ceiling(content.ActualHeight));
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
            dc.DrawRectangle(new VisualBrush(content) { Stretch = Stretch.Fill }, null, new Rect(0, 0, width, height));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); File.WriteAllBytes(path, ImageService.Png(bitmap));
    }

    private static void Assert(bool result, string name) { if (!result) throw new InvalidOperationException(name + " failed."); }
}
