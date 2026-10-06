using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Interop;
using SnippyGrab.App.Views;

namespace SnippyGrab.App.Services;

internal static class RuntimeChecks
{
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
            var image = SyntheticCode(320, 160); var png = ImageService.Png(image);
            var record = controller.Repository.Add(png, 320, 160);
            var history = new HistoryWindow(controller) { ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000 };
            var pin = new PinWindow(controller, record) { ShowActivated = false, Topmost = false, Left = -30000, Top = -30000 };
            try
            {
                history.Show(); pin.Show(); await Task.Delay(30);
                var list = ((DockPanel)history.Content).Children.OfType<ListBox>().Single(); list.SelectedIndex = 0;
                controller.Repository.Add(png, 320, 160); Assert(list.Items.Count == 2 && list.SelectedItems.Count == 1, "Open history refresh preserves selection after capture");
                controller.Repository.Replace(record, ImageService.Png(SyntheticCode(640, 240)), 640, 240);
                var historyImage = ((DockPanel)history.Content).Children.OfType<Image>().Single();
                var pinImage = (Image)((Border)pin.Content).Child;
                Assert(((BitmapSource)historyImage.Source).PixelWidth == 600 && ((BitmapSource)pinImage.Source).PixelWidth == 640, $"History and detached pin refresh committed pixels (history={((BitmapSource)historyImage.Source).PixelWidth}, pin={((BitmapSource)pinImage.Source).PixelWidth})");
                controller.Repository.SetPinned(record, false); controller.Repository.Cleanup(DateTimeOffset.UtcNow, 1, true);
                Assert(File.Exists(controller.Repository.PathFor(record)), "Unpinned open view protects current source");
                pin.Close(); controller.Repository.Cleanup(DateTimeOffset.UtcNow, 1, true); Assert(list.Items.Count == 0, "Pin close releases source and history removes cleaned rows");

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
                Assert(Native.GetForegroundWindow() == foreground, "Reliability probe preserves foreground");
            }
            finally { pin.Close(); history.Close(); controller.Dock.Close(); }
            AtomicFile.Write(destination, JsonSerializer.SerializeToUtf8Bytes(new { Result = "PASS", Scope = "Offscreen synthetic history/pin revision and lease lifecycle, injected OCR/clipboard; no pointer movement, real capture, OS clipboard write or startup registration change." }));
        }
        finally
        {
            var absolute = Path.GetFullPath(root);
            if (!absolute.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(absolute).StartsWith("SnippyGrab-reliability-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected reliability probe path.");
            Directory.Delete(absolute, true);
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
            var editor = new EditorWindow(controller, record) { ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000 };
            try
            {
                editor.Show(); await Task.Delay(50);
                foreach (var textScale in new[] { 1.0, 1.5, 2.25 })
                foreach (var width in new[] { 660, 1000 })
                {
                    Application.Current.Resources["BodyTextSize"] = 13 * textScale;
                    Application.Current.Resources["TextSize.12"] = 12 * textScale;
                    ((DockPanel)editor.Content).Children.OfType<WrapPanel>().Last().Children.OfType<ComboBox>().Single().SelectedValue = EditTool.OcrArea;
                    editor.Width = width; editor.UpdateLayout(); await Task.Delay(25);
                    var rootPanel = (DockPanel)editor.Content;
                    var toolbar = rootPanel.Children.OfType<WrapPanel>().First();
                    var buttons = toolbar.Children.OfType<Button>().ToArray();
                    Assert(buttons.Any(b => Equals(b.Content, "Apply + copy")) && buttons.Any(b => Equals(b.Content, "Export PNG…")) && !buttons.Any(b => Equals(b.Content, "Save")), "Explicit apply/export labels");
                    Assert(buttons.Single(b => Equals(b.Content, "Open export folder")).IsEnabled, "Successful export exposes folder action");
                    foreach (var control in rootPanel.Children.OfType<WrapPanel>().SelectMany(p => p.Children.OfType<FrameworkElement>()))
                    {
                        var bounds = control.TransformToAncestor(rootPanel).TransformBounds(new Rect(control.RenderSize));
                        Assert(bounds.Left >= -1 && bounds.Right <= rootPanel.ActualWidth + 1 && bounds.Bottom <= rootPanel.ActualHeight, $"Every editor action/tool stays reachable at {width} DIP and {textScale:P0} text");
                    }
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
                        dock.SetExpanded(false); dock.Reveal(); await Task.Delay(35); dock.UpdateLayout();
                        var panel = (StackPanel)((Border)dock.Content).Child;
                        var id = controller.Repository.Captures[0].Id;
                        Border Primary() => panel.Children.OfType<Border>().Single(b => b.Tag is Guid guid && guid == id);
                        var before = Primary().PointToScreen(new Point(Primary().ActualWidth / 2, Primary().ActualHeight / 2));
                        var buildsBefore = dock.CardBuildCount; var decodesBefore = dock.ThumbnailDecodeCount;
                        dock.SetExpanded(true); await Task.Delay(35); dock.UpdateLayout();
                        var after = Primary().PointToScreen(new Point(Primary().ActualWidth / 2, Primary().ActualHeight / 2));
                        Assert(Math.Abs(before.X - after.X) <= 1 && Math.Abs(before.Y - after.Y) <= 1, "Primary card anchor survives expansion");
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
                        for (var i = 0; i < 5; i++) dock.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
                        Assert(dock.RebuildCount == rebuilds, "Repeated enter does not rebuild expanded cards");
                        dock.ToggleSelection(id);
                        dock.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
                        await Task.Delay(220);
                        Assert(!dock.Expanded && dock.SelectionCount == 1, "Pointer leave collapses without losing selection");
                        dock.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
                        Assert(dock.Expanded && dock.SelectionCount == 1, "Re-entry preserves expansion and selection");
                        Assert(dock.CachedCardCount <= 5 && dock.CachedThumbnailCount <= 12, "Bounded dock caches");
                        dock.ToggleSelection(id);
                        results.Add(new { count, corner, orientation, PrimaryAnchorStable = true, WarmCycleCardBuilds = dock.CardBuildCount - buildsBefore, ThumbnailDecodes = dock.ThumbnailDecodeCount - decodesBefore });
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
            dock.CommandSinkOverride = null;
            Snapshot(dock, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination))!, "dock-layout-synthetic.png"));
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
            window.Show(); window.UpdateLayout(); await Task.Delay(200); Native.DwmFlush();
            Native.GetWindowRect(new WindowInteropHelper(window).Handle, out var rect);
            timer.Restart(); var captured = ImageService.Capture(rect.Pixels, false); var bytes = ImageService.Png(captured); var captureTime = timer.ElapsedMilliseconds;
            Assert(captured.PixelWidth == rect.Pixels.Width && bytes.Length > 0, "Native physical capture");
            // Confirm pixels rather than inferring rendering from a HWND alone.
            var center = new byte[4]; captured.CopyPixels(new Int32Rect(captured.PixelWidth / 2, captured.PixelHeight / 2, 1, 1), center, 4, 0);
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
    private static BitmapSource SyntheticCode(int width, int height)
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
