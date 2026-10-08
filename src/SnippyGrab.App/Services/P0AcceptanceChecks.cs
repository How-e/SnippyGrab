using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Threading;
using SnippyGrab.App.Views;

namespace SnippyGrab.App.Services;

// Explicit opt-in acceptance lane: actual production dialogs, registered editors,
// tray Exit and OS clipboard, with synthetic captures and an isolated cache only.
internal static class P0AcceptanceChecks
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    internal static AppController Open(string report, string? existingRoot)
    {
        var root = Path.GetFullPath(existingRoot ?? Path.Combine(Path.GetTempPath(), "SnippyGrab-p0-" + Guid.NewGuid().ToString("N")));
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!root.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("SnippyGrab-p0-", StringComparison.Ordinal))
            throw new InvalidDataException("P0 checks require their own temporary root.");
        if (existingRoot is not null && !File.Exists(Path.Combine(root, "p0-fixture.json")))
            throw new InvalidDataException("Existing root is not a P0 fixture.");
        var fresh = existingRoot is null;
        if (fresh)
        {
            Directory.CreateDirectory(root);
            AtomicFile.Write(Path.Combine(root, "p0-fixture.json"), JsonSerializer.SerializeToUtf8Bytes(new { Scope = "Synthetic P0 acceptance" }));
            new SettingsService(Path.Combine(root, "settings.json")).Save(new Settings { FirstRunComplete = true, Animate = false, AutoCopy = false, DockLifetimeMinutes = 0 });
        }
        var controller = new AppController(true, root, diagnostic: true);
        if (fresh)
        {
            for (var n = 1; n <= 2; n++)
            {
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 720, 360));
                    dc.DrawText(new System.Windows.Media.FormattedText($"P0 SYNTHETIC {n}\nEdits must survive failure and retry", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), 28, Brushes.Black, 1), new Point(30, 40));
                }
                var image = new RenderTargetBitmap(720, 360, 96, 96, PixelFormats.Pbgra32); image.Render(visual); image.Freeze();
                controller.Repository.Add(ImageService.Png(image), 720, 360);
            }
            File.WriteAllText(Path.Combine(controller.Repository.Root, "history.json"), "P0 synthetic unreadable history");
            controller.Repository.Load();
        }
        controller.ShowAcceptanceTray();
        var events = new List<object>();
        FileStream? metadataLock = null;
        CancellationTokenSource? clipboardLock = null;
        var clipboardHeld = false;
        var window = new Window { Title = "SnippyGrab · P0 acceptance controls", Width = 780, Height = 440, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        Ui.StyleWindow(window);
        var panel = new StackPanel { Margin = new Thickness(18) }; window.Content = panel;
        panel.Children.Add(Ui.Text("P0 acceptance · isolated synthetic cache", 22));
        panel.Children.Add(new TextBlock { Text = "Real recovery dialogs, registered editors and tray Exit. Hotkeys/startup disabled.\nApply/copy writes synthetic pixels to the real OS clipboard. Clipboard lock auto-releases after 45 seconds.\nUse the SnippyGrab P0 acceptance tray icon for the actual Exit test.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) });
        var buttons = new WrapPanel(); panel.Children.Add(buttons);
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) }; panel.Children.Add(status);
        void Note(string action) { events.Add(new { Action = action, Utc = DateTimeOffset.UtcNow }); Save(); }
        void Button(string label, Action action) => buttons.Children.Add(Ui.Button(label, label, () => controller.Try(() => { action(); Note(label); })));
        Button("History recovery", controller.ShowHistory);
        Button("Block history write", () => { metadataLock ??= new FileStream(Path.Combine(controller.Repository.Root, "history.json"), FileMode.Open, FileAccess.Read, FileShare.Read); });
        Button("Release history write", () => { metadataLock?.Dispose(); metadataLock = null; });
        Button("Edit first", () => controller.Edit(controller.Repository.Captures[0]));
        Button("Edit second", () => controller.Edit(controller.Repository.Captures[1]));
        Button("Prepare two dirty editors", () => controller.Run(async () =>
        {
            foreach (var capture in controller.Repository.Captures.Take(2)) controller.Edit(capture);
            foreach (var editor in controller.AcceptanceEditors)
                await editor.StartDocumentEdit(new(EditTool.Redact, new Point(30, 150), new Point(170, 230), Colors.Black, 3, 24, "", []));
            Note("Both registered editors have pending redaction edits");
        }));
        Button("Apply while exiting", () => controller.Run(async () =>
        {
            var editor = controller.AcceptanceEditors.FirstOrDefault() ?? throw new InvalidOperationException("Open editors first.");
            var work = editor.StartDocumentEdit(new(EditTool.Blur, new Point(20, 20), new Point(700, 340), Colors.Black, 3, 24, "", []));
            Note("Worker edit started immediately before production Exit");
            controller.Exit(); await work;
        }));
        Button("Lock OS clipboard 45s", () =>
        {
            if (clipboardLock is not null) throw new InvalidOperationException("Release the previous clipboard lock first.");
            var cancellation = clipboardLock = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            _ = Task.Run(() =>
            {
                var opened = OpenClipboard(IntPtr.Zero);
                Application.Current.Dispatcher.Invoke(() => { clipboardHeld = opened; Note(opened ? "Real OS clipboard opened exclusively" : "FAIL: OS clipboard lock unavailable"); });
                try { if (opened) cancellation.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(45)); }
                finally
                {
                    if (opened) CloseClipboard();
                    Application.Current.Dispatcher.Invoke(() => { clipboardHeld = false; Note("OS clipboard lock released"); });
                }
            });
        });
        Button("Release OS clipboard", () => { clipboardLock?.Cancel(); clipboardLock = null; });
        Button("Inspect clipboard pixels", () =>
        {
            var copied = Clipboard.GetImage() ?? throw new InvalidDataException("OS clipboard has no image.");
            // The observed Windows bitmap clipboard changes alpha on text pixels.
            // Verify all bitmap color channels, and independently verify lossless PNG alpha.
            var copiedHash = PixelHash(copied, ignoreAlpha: true);
            var matches = controller.Repository.Captures.Where(c => PixelHash(ImageService.Load(controller.Repository.PathFor(c)), ignoreAlpha: true) == copiedHash).Select(c => c.Id).ToArray();
            var pngStream = Clipboard.GetData("PNG") as Stream ?? throw new InvalidDataException("OS clipboard has no PNG stream.");
            pngStream.Position = 0;
            var pngImage = BitmapDecoder.Create(pngStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            var pngHash = PixelHash(pngImage);
            var pngMatches = controller.Repository.Captures.Where(c => PixelHash(ImageService.Load(controller.Repository.PathFor(c))) == pngHash).Select(c => c.Id).ToArray();
            events.Add(new { Action = "OS clipboard pixel verification", Matches = matches, PngMatches = pngMatches, copied.PixelWidth, copied.PixelHeight, Utc = DateTimeOffset.UtcNow });
            if (matches.Length == 0) throw new InvalidDataException("OS clipboard pixels do not match a current synthetic capture.");
            if (!matches.SequenceEqual(pngMatches)) throw new InvalidDataException("Bitmap colors and PNG pixels disagree.");
        });
        Button("Exit controls", controller.Exit);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Save(); timer.Start();
        controller.Repository.Changed += Save;
        Application.Current.Exit += (_, _) =>
        {
            timer.Stop(); metadataLock?.Dispose(); clipboardLock?.Cancel();
            events.Add(new { Action = "Production application Exit", Utc = DateTimeOffset.UtcNow }); Save();
        };
        window.Closed += (_, _) => controller.Exit();
        window.Show(); Save();
        return controller;

        void Save()
        {
            status.Text = $"Recovery blocked: {controller.Repository.CleanupBlocked} · History lock: {metadataLock is not null} · Clipboard lock: {clipboardHeld}\nOpen registered editors: {controller.AcceptanceEditors.Count} · Exiting: {controller.Exiting}\nLast operation: {controller.LastOperationDetails}";
            AtomicFile.Write(report, JsonSerializer.SerializeToUtf8Bytes(new
            {
                Build = BuildVersion.Display,
                Root = root,
                Scope = "Actual native production dialogs/editors/tray and OS clipboard; isolated synthetic cache, hotkeys/startup disabled",
                controller.Repository.CleanupBlocked,
                HistoryLocked = metadataLock is not null,
                ClipboardLocked = clipboardHeld,
                OpenEditors = controller.AcceptanceEditors.Count,
                controller.Exiting,
                LastOperation = controller.LastOperationDetails,
                Captures = controller.Repository.Captures.Select(c => new { c.Id, c.FileName, c.Edited, c.Pinned, Exists = File.Exists(controller.Repository.PathFor(c)), Hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(controller.Repository.PathFor(c)))) }).ToArray(),
                PreservedOriginals = Directory.GetFiles(controller.Repository.Root, "history.invalid-*.json").Select(p => new { Name = Path.GetFileName(p), Hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray(),
                Events = events.ToArray()
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static string PixelHash(BitmapSource source, bool ignoreAlpha = false)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = checked(converted.PixelWidth * 4);
        var pixels = new byte[checked(stride * converted.PixelHeight)]; converted.CopyPixels(pixels, stride, 0);
        if (ignoreAlpha) for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
        return Convert.ToHexString(SHA256.HashData(pixels));
    }
}
