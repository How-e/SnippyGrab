using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Windows.Interop;
using SnippyGrab.App.Views;

namespace SnippyGrab.App.Services;

internal static class RuntimeChecks
{
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
        Assert(controller.Dock.ActualWidth < 280 && controller.Dock.ActualHeight < 210, "Compact 20-capture shelf");
        var settings = new SettingsWindow(controller, true); settings.Show(); await Task.Delay(100); Snapshot(settings, Path.Combine(output, "settings.png")); settings.Close();
        var welcome = new WelcomeWindow(controller); welcome.Show(); await Task.Delay(100); Snapshot(welcome, Path.Combine(output, "welcome.png")); welcome.Close();
        var editor = new EditorWindow(controller, controller.Repository.Captures[0]); editor.Show(); await Task.Delay(100); Snapshot(editor, Path.Combine(output, "editor.png")); editor.Close();
        var history = new HistoryWindow(controller); history.Show(); await Task.Delay(100); Snapshot(history, Path.Combine(output, "history.png")); history.Close();
        await Task.Delay(2000);
        GC.Collect(); GC.WaitForPendingFinalizers(); await Task.Delay(2000);
        var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime; var before = process.WorkingSet64;
        await Task.Delay(5000); process.Refresh();
        var result = new { Captures = 20, ShelfWidthDip = controller.Dock.ActualWidth, ShelfHeightDip = controller.Dock.ActualHeight, WorkingSetMb = Math.Round(process.WorkingSet64 / 1048576.0, 1), IdleCpuMsOver5Seconds = (process.TotalProcessorTime - cpu).TotalMilliseconds, WorkingSetDeltaMb = Math.Round((process.WorkingSet64 - before) / 1048576.0, 1), Screenshots = "synthetic dock, editor, settings and history" };
        controller.Dispose(); controller.Dock.Close(); return result;
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
