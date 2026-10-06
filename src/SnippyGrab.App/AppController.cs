using System.Diagnostics;
using System.Windows.Threading;
using Microsoft.Win32;
using SnippyGrab.App.Services;
using SnippyGrab.App.Views;
using Forms = System.Windows.Forms;

namespace SnippyGrab.App;

internal enum ExportStatus { Cancelled, Exported, Failed }
internal sealed record ExportOutcome(ExportStatus Status, string Path = "", string? Message = null);

internal sealed class AppController : IDisposable
{
    public Settings Settings { get; private set; }
    public CaptureRepository Repository { get; }
    public ClipboardService Clipboard { get; } = new();
    public DragDropService DragDrop { get; }
    public IOcrService OcrService { get; } = new OcrService();
    public HotkeyService Hotkeys { get; } = new();
    public DockWindow Dock { get; }
    public bool Exiting { get; private set; }
    private readonly SettingsService settingsService;
    private readonly CaptureService capture = new();
    private readonly Forms.NotifyIcon tray;
    private readonly DispatcherTimer cleanup = new();
    private readonly DispatcherTimer expiry = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly Dictionary<Guid, EditorWindow> editors = [];
    private readonly Dictionary<Guid, PinWindow> pins = [];
    private SettingsWindow? settingsWindow;
    private HistoryWindow? historyWindow;
    private WelcomeWindow? welcomeWindow;
    private readonly string dataDirectory;
    private bool dockWasVisible;
    private bool disposed;
    private bool exitRequested;
    public AppController(bool background, string? isolatedDataDirectory = null, bool diagnostic = false)
    {
        dataDirectory = isolatedDataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnippyGrab");
        Directory.CreateDirectory(dataDirectory);
        settingsService = new(Path.Combine(dataDirectory, "settings.json")); Settings = settingsService.Load();
        Ui.Theme(Settings.Theme);
        Settings.LaunchOnStartup = StartupService.Enabled;
        try { Repository = new(Settings.CachePath.Length == 0 ? Path.Combine(dataDirectory, "cache") : Settings.CachePath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        { Repository = new(Path.Combine(dataDirectory, "cache")); Settings.CachePath = ""; }
        Repository.HistoryEnabled = Settings.HistoryEnabled; Repository.Load();
        DragDrop = new(Repository); Dock = new(this);
        tray = new Forms.NotifyIcon { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application, Text = "SnippyGrab · Print Screen to capture", Visible = !diagnostic };
        tray.DoubleClick += (_, _) => Run(() => Capture(CaptureMode.Region));
        Hotkeys.Capture += mode => Run(() => Capture(mode));
        Hotkeys.Configure(Settings, diagnostic); BuildTray();
        cleanup.Tick += (_, _) => Try(() => { Repository.Cleanup(DateTimeOffset.UtcNow, Settings.RetentionHours); Dock.Refresh(); });
        cleanup.Interval = TimeSpan.FromMinutes(Settings.CleanupMinutes); cleanup.Start();
        expiry.Tick += (_, _) => { if (Repository.Captures.Any(c => !c.Dismissed)) Dock.Refresh(); }; expiry.Start();
        Try(() => Repository.Cleanup(DateTimeOffset.UtcNow, Settings.RetentionHours));
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged += PowerChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += PreferencesChanged;
        if (!Settings.FirstRunComplete) ShowWelcome();
        else if (!background && !Settings.StartMinimized) ShowHistory();
        else if (Hotkeys.Warnings.Count > 0) Notify(string.Join("\n", Hotkeys.Warnings));
        if (settingsService.Recovered || Repository.Recovered) Notify(Repository.CleanupBlocked ? "History needs recovery. Open Recent captures to review and confirm. Unknown old captures are pinned; cleanup is disabled and the original history is preserved." : "Recovered invalid local settings. Review Settings before continuing.");
    }
    public async Task Capture(CaptureMode mode)
    {
        if (capture.Busy || exitRequested || Exiting) return;
        var result = await capture.CaptureAsync(mode, Settings.IncludeCursor,
            () => { dockWasVisible = Dock.IsVisible; Dock.Hide(); foreach (var pin in pins.Values) pin.Hide(); },
            () => { if (dockWasVisible) Dock.Reveal(); foreach (var pin in pins.Values) pin.Show(); });
        if (result is null || exitRequested || Exiting) return;
        var ready = Stopwatch.StartNew();
        var png = ImageService.Png(result.Image);
        var record = Repository.Add(png, result.Image.PixelWidth, result.Image.PixelHeight, $"{result.Bounds.X},{result.Bounds.Y}");
        Dock.Refresh(newCapture: true);
        if (Settings.AutoCopy) await CopyImageCore(result.Image, png);
        Log($"capture_ready_ms={ready.ElapsedMilliseconds}; capture_total_ms={result.ElapsedMilliseconds + ready.ElapsedMilliseconds}");
    }
    public Task<bool> CopyImage(BitmapSource image) => CopyImageCore(image);
    private async Task<bool> CopyImageCore(BitmapSource image, byte[]? encoded = null)
    {
        var success = await Clipboard.ImageAsync(image, Settings.ClipboardPng, encoded);
        if (!success) Notify("Clipboard is busy. Capture is in the shelf; use Copy again."); return success;
    }
    public async Task Copy(CaptureRecord record)
    {
        using var lease = Repository.Lease([record]); await CopyImage(ImageService.Load(Repository.PathFor(record)));
    }
    public async Task CopyFiles(IReadOnlyList<CaptureRecord> records)
    {
        var ordered = TransferPayload.Ordered(Repository, records);
        using var lease = Repository.Lease(ordered, transfer: true);
        if (!await Clipboard.FilesAsync(TransferPayload.Files(Repository, ordered))) Notify("Clipboard is busy. Try Copy again.");
    }
    public async Task Ocr(CaptureRecord record)
    {
        using var lease = Repository.Lease([record]);
        var image = ImageService.Load(Repository.PathFor(record));
        var text = await OcrService.ReadAsync(ImageService.Png(image));
        if (string.IsNullOrWhiteSpace(text)) { Notify("No text found in this capture."); return; }
        if (!await Clipboard.TextAsync(text)) Notify("Clipboard is busy. Try OCR again."); else Notify("OCR text copied.");
    }
    public void Edit(CaptureRecord record) => Try(() =>
    {
        if (exitRequested) return;
        if (editors.TryGetValue(record.Id, out var existing)) { existing.Activate(); return; }
        var editor = new EditorWindow(this, record); editors[record.Id] = editor;
        editor.Closed += (_, _) => editors.Remove(record.Id); editor.Show();
    });
    public ExportOutcome Save(CaptureRecord record, Window? owner = null)
    {
        try
        {
            var priorDirectory = string.IsNullOrWhiteSpace(record.ExportPath) ? null : Path.GetDirectoryName(record.ExportPath);
            var directory = Directory.Exists(priorDirectory) ? priorDirectory! : Directory.Exists(Settings.SaveDirectory) ? Settings.SaveDirectory : "";
            var dialog = new SaveFileDialog { Title = "Export PNG outside the managed cache", Filter = "PNG image|*.png", FileName = string.IsNullOrWhiteSpace(record.ExportPath) ? $"SnippyGrab-{record.CreatedUtc.LocalDateTime:yyyyMMdd-HHmmss}.png" : Path.GetFileName(record.ExportPath), DefaultExt = ".png", AddExtension = true, OverwritePrompt = true, InitialDirectory = directory };
            if ((owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) != true) return new(ExportStatus.Cancelled);
            var result = CaptureExport.Write(Repository, record, dialog.FileName);
            var warning = result.MetadataSaved ? null : "PNG exported, but capture history could not be updated. The file is safe; retry later to remember its destination.";
            Notify(warning ?? "PNG exported to: " + result.Path);
            Try(() => Dock.Refresh());
            return new(ExportStatus.Exported, result.Path, warning);
        }
        catch (Exception ex)
        {
            Failure(ex); return new(ExportStatus.Failed, Message: ex.Message);
        }
    }
    public void OpenExportFolder(string path) => Try(() =>
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!Directory.Exists(directory)) throw new IOException("The export folder is no longer available.");
        Process.Start(new ProcessStartInfo { FileName = directory!, UseShellExecute = true });
    });
    public void Pin(CaptureRecord record) => Try(() => { record.Pinned = !record.Pinned; Repository.Persist(); Dock.Refresh(); });
    public void Detach(CaptureRecord record) => Try(() =>
    {
        record.Pinned = true; Repository.Persist();
        if (pins.TryGetValue(record.Id, out var existing)) { existing.RestoreInteraction(); return; }
        var pin = new PinWindow(this, record); pins[record.Id] = pin; pin.Closed += (_, _) => pins.Remove(record.Id); pin.Show();
    });
    public void Dismiss(IEnumerable<CaptureRecord> records) => Try(() => { foreach (var c in records) c.Dismissed = true; Repository.Persist(); Dock.Refresh(); });
    public void Reorder(Guid from, Guid target) => Try(() =>
    {
        // Shelf order is persisted directly in the repository list; history sorts by timestamp independently.
        var items = (List<CaptureRecord>)Repository.Captures;
        if (!ShelfOrder.Move(items, from, target)) return;
        Repository.Persist(); Dock.Refresh();
    });
    public void Import(string path) => Try(() =>
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".bmp")) throw new InvalidDataException("Only PNG, JPEG and BMP images are accepted.");
        var image = ImageService.Load(path); Repository.Add(ImageService.Png(image), image.PixelWidth, image.PixelHeight); Dock.Refresh(true);
    });
    public void ClearTemporary() => Try(() => { if (Repository.CleanupBlocked) { Notify("Cleanup is disabled because history is unreadable. Original metadata is preserved; recover it before deleting captures."); return; } var count = Repository.Cleanup(DateTimeOffset.UtcNow, Settings.RetentionHours, clear: true); Dock.Refresh(); Notify($"Cleared {count} temporary capture(s). Pins and transfers are protected."); });
    private void ShowWelcome()
    {
        var wasPaused = Hotkeys.Paused; Hotkeys.Configure(Settings, true);
        welcomeWindow = new(this); welcomeWindow.Closed += (_, _) => { welcomeWindow = null; Hotkeys.Configure(Settings, wasPaused); BuildTray(); }; welcomeWindow.Show();
    }
    public void ShowSettings() => ShowSettings(false);
    private void ShowSettings(bool welcome)
    {
        if (settingsWindow is not null) { settingsWindow.Activate(); return; }
        var wasPaused = Hotkeys.Paused; Hotkeys.Configure(Settings, true);
        settingsWindow = new(this, welcome); settingsWindow.Closed += (_, _) => { settingsWindow = null; Hotkeys.Configure(Settings, welcomeWindow is not null || wasPaused); BuildTray(); }; settingsWindow.Show();
    }
    public void ShowHistory()
    {
        if (historyWindow is not null) { historyWindow.Activate(); return; }
        historyWindow = new(this); historyWindow.Closed += (_, _) => historyWindow = null; historyWindow.Show();
    }
    public void ApplySettings(Settings settings)
    {
        StartupService.Set(settings.LaunchOnStartup); settingsService.Save(settings); Settings = settings;
        Repository.HistoryEnabled = settings.HistoryEnabled; Repository.Persist();
        Ui.Theme(settings.Theme); Hotkeys.Configure(settings, Hotkeys.Paused); cleanup.Interval = TimeSpan.FromMinutes(settings.CleanupMinutes); Dock.Refresh(); BuildTray();
        if (Hotkeys.Warnings.Count > 0) Notify(string.Join("\n", Hotkeys.Warnings));
    }
    private void BuildTray()
    {
        var menu = new Forms.ContextMenuStrip();
        void Item(string label, Action action, bool check = false) { var item = new Forms.ToolStripMenuItem(label) { Checked = check }; item.Click += (_, _) => Try(action); menu.Items.Add(item); }
        Item("Capture region", () => Run(() => Capture(CaptureMode.Region)));
        Item("Capture window", () => Run(() => Capture(CaptureMode.Window)));
        Item("Capture active window", () => Run(() => Capture(CaptureMode.ActiveWindow)));
        Item("Capture entire desktop", () => Run(() => Capture(CaptureMode.Desktop)));
        menu.Items.Add(new Forms.ToolStripSeparator());
        Item("Show screenshot shelf", Dock.Reveal); Item("Focus screenshot shelf (keyboard)", Dock.FocusShelf); Item("Open recent captures", ShowHistory); Item("Hotkey help / conflicts", () => MessageBox.Show(HotkeyRegistration.Guidance(Settings) + "\n\n" + string.Join("\n", Hotkeys.Warnings), "SnippyGrab · Hotkey help")); Item("Open settings", ShowSettings);
        Item("Restore pins", () => { foreach (var pin in pins.Values) pin.RestoreInteraction(); });
        Item("Pause hotkeys", () => { Hotkeys.Configure(Settings, !Hotkeys.Paused); BuildTray(); }, Hotkeys.Paused);
        Item("Clear temporary screenshots", ClearTemporary);
        Item("Launch at Windows login", () => { Settings.LaunchOnStartup = !StartupService.Enabled; ApplySettings(Settings); }, StartupService.Enabled);
        Item("About", () => MessageBox.Show("SnippyGrab 0.1.0 alpha\nNative, local screenshot shelf. MIT licensed.\nNo uploads, accounts, analytics or update polling.\n\nPrint Screen: region · Ctrl+Shift+S: fallback\nCtrl-click: select several · Drag: attach files\nClick: edit · Alt-drag: reorder\n\nUnsigned development build. See README for verification and limitations.", "SnippyGrab"));
        menu.Items.Add(new Forms.ToolStripSeparator()); Item("Exit", Exit);
        var old = tray.ContextMenuStrip; tray.ContextMenuStrip = menu; old?.Dispose();
    }
    private void DisplayChanged(object? sender, EventArgs e) => Application.Current.Dispatcher.BeginInvoke(() => { Dock.Refresh(); Dock.Reveal(); });
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Resume) Application.Current.Dispatcher.BeginInvoke(() => Try(() => { Hotkeys.Configure(Settings, Hotkeys.Paused); Repository.Cleanup(DateTimeOffset.UtcNow, Settings.RetentionHours); Dock.Refresh(); })); }
    private void PreferencesChanged(object sender, UserPreferenceChangedEventArgs e) => Application.Current.Dispatcher.BeginInvoke(() => Ui.Theme(Settings.Theme));
    public async void Run(Func<Task> action) { try { await action(); } catch (Exception ex) { Failure(ex); } }
    public void Try(Action action) { try { action(); } catch (Exception ex) { Failure(ex); } }
    private void Failure(Exception ex) { Log("failure=" + ex.GetType().Name); Notify(ex is DllNotFoundException or TypeInitializationException ? "Local OCR could not load. Install the Microsoft Visual C++ 2015–2022 x64 runtime and use the complete release package." : ex.Message); }
    private void Log(string text)
    {
        try { var path = Path.Combine(dataDirectory, "diagnostics.log"); if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024) File.Delete(path); File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {text}\n"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    public void Notify(string message) { if (!Exiting) tray.ShowBalloonTip(3500, "SnippyGrab", message.Length > 250 ? message[..250] : message, Forms.ToolTipIcon.Info); }
    public void Exit()
    {
        if (Exiting || exitRequested) return;
        Run(ExitAsync);
    }
    private async Task ExitAsync()
    {
        exitRequested = true;
        var wasPaused = Hotkeys.Paused;
        Hotkeys.Configure(Settings, true);
        try
        {
            foreach (var editor in editors.Values.ToArray())
                if (!await editor.RequestCloseAsync()) return;
            if (Settings.SessionOnly) Repository.Cleanup(DateTimeOffset.UtcNow, Settings.RetentionHours, true);
            else Repository.Persist();
            Exiting = true;
            Application.Current.Shutdown();
        }
        finally
        {
            exitRequested = false;
            if (!Exiting) Hotkeys.Configure(Settings, wasPaused);
        }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        Exiting = true; cleanup.Stop(); expiry.Stop();
        SystemEvents.DisplaySettingsChanged -= DisplayChanged; SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.UserPreferenceChanged -= PreferencesChanged;
        Hotkeys.Dispose(); tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Icon?.Dispose(); tray.Dispose();
    }
}
