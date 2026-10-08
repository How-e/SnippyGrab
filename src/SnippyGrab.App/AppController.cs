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
    public ClipboardService Clipboard { get; internal set; } = new();
    public DragDropService DragDrop { get; }
    public IOcrService OcrService { get; internal set; }
    internal event Action? SettingsChanged;
    public HotkeyService Hotkeys { get; } = new();
    public DockWindow Dock { get; }
    public bool Exiting { get; private set; }
    internal bool Diagnostic { get; }
    internal IReadOnlyCollection<EditorWindow> AcceptanceEditors => editors.Values.ToArray();
    internal void ShowAcceptanceTray()
    {
        if (!Diagnostic) throw new InvalidOperationException("Acceptance tray requires diagnostic isolation.");
        tray.Text = "SnippyGrab P0 acceptance";
        tray.Visible = true;
    }
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
    private readonly LifecycleDispatch lifecycle;
    private readonly HotkeyPauseState hotkeyPause = new();
    private readonly LatestOperation ocr = new();
    private bool exitRequested;
    private string? cacheWarning;
    private string lastNotice = "No recent notification.";
    internal string LastOperationDetails => lastNotice;
    private DateTimeOffset lastNoticeUtc;
    public AppController(bool background, string? isolatedDataDirectory = null, bool diagnostic = false, Action<string, byte[]>? storageWriter = null)
    {
        Diagnostic = diagnostic;
        lifecycle = new(action => Application.Current.Dispatcher.BeginInvoke(action));
        dataDirectory = isolatedDataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnippyGrab");
        ManagedPath.RejectRedirects(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        settingsService = new(Path.Combine(dataDirectory, "settings.json")); Settings = settingsService.Load();
        if (diagnostic) Settings.CachePath = "";
        OcrService = new OcrService(settingsProvider: () => Settings);
        if (Settings.DockMonitor >= 0 && Settings.DockMonitorIdentity.Length == 0)
        {
            Settings.DockMonitorIdentity = MonitorService.All().ElementAtOrDefault(Settings.DockMonitor)?.Identity ?? "";
            try { settingsService.Save(Settings); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { cacheWarning = "Monitor identity could not be saved. Review and retry Settings before changing the display layout."; }
        }
        Ui.FailureHandler = Failure;
        capture.Timing += Log;
        Ui.Theme(Settings.Theme);
        Settings.LaunchOnStartup = !diagnostic && StartupService.Enabled;
        if (!diagnostic && StartupService.Stale) cacheWarning = "Windows startup points to another or older SnippyGrab path. Enable Launch at Windows login in Settings to register this executable, or disable it to remove the old entry.";
        try { Repository = new(Settings.CachePath.Length == 0 ? Path.Combine(dataDirectory, "cache") : Settings.CachePath, storageWriter); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        { Repository = new(Path.Combine(dataDirectory, "cache"), storageWriter); Settings.CachePath = ""; cacheWarning = "Custom cache is unavailable. Using the default local cache; review Settings and retry the custom path."; }
        Repository.HistoryEnabled = Settings.HistoryEnabled; Repository.Load();
        DragDrop = new(Repository); Dock = new(this);
        tray = new Forms.NotifyIcon { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application, Text = "SnippyGrab · Print Screen to capture", Visible = !diagnostic };
        Repository.PersistenceFailed += () => Notify("Capture pixels are safe in the cache and shelf, but history could not be saved. Cleanup is paused; retry via tray → Retry history save.");
        if (cacheWarning is not null) Notify(cacheWarning);
        tray.DoubleClick += (_, _) => Run(() => Capture(Settings.DefaultCaptureMode));
        Hotkeys.TaskbarRestored += () => lifecycle.Post(() => { if (!diagnostic) { tray.Visible = false; tray.Visible = true; } });
        Hotkeys.Capture += mode => Run(() => Capture(mode));
        ConfigureHotkeys(false); BuildTray();
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
        ocr.Cancel(); Clipboard.Invalidate();
        var result = await capture.CaptureAsync(mode, Settings.IncludeCursor,
            () => { dockWasVisible = Dock.IsVisible; Dock.Hide(); foreach (var pin in pins.Values) pin.Hide(); },
            () => { if (dockWasVisible) Dock.Reveal(); foreach (var pin in pins.Values) pin.Show(); });
        if (result is null || exitRequested || Exiting) return;
        var ready = Stopwatch.StartNew();
        var png = await Task.Run(() => ImageService.Png(result.Image));
        await PreserveCaptureAsync(result.Image, png, string.Join(",", Forms.Screen.AllScreens.Where(screen => !result.Bounds.Intersect(new PixelRect(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height)).IsEmpty).Select(screen => screen.DeviceName)), () => Dock.Refresh(newCapture: true));
        Log($"capture_ready_ms={ready.ElapsedMilliseconds}; capture_total_ms={result.ElapsedMilliseconds + ready.ElapsedMilliseconds}");
    }
    internal async Task PreserveCaptureAsync(BitmapSource image, byte[] png, string monitor, Action refreshDock)
    {
        try { Repository.Add(png, image.PixelWidth, image.PixelHeight, monitor); refreshDock(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (Settings.AutoCopy && await Clipboard.ImageAsync(image, Settings.ClipboardPng, png)) Notify("Capture copied, but cache storage failed. Paste it now; check cache permissions/free space before retrying capture.");
            else Notify("Capture storage and clipboard could not preserve this image. Check cache permissions/free space and retry capture.");
            return;
        }
        if (Settings.AutoCopy) await CopyImageCore(image, png);
    }
    public Task<bool> CopyImage(BitmapSource image) => CopyImageCore(image);
    private async Task<bool> CopyImageCore(BitmapSource image, byte[]? encoded = null)
    {
        ocr.Cancel();
        var success = await Clipboard.ImageAsync(image, Settings.ClipboardPng, encoded);
        if (!success) Notify("Clipboard is busy. Capture is in the shelf; use Copy again."); return success;
    }
    public async Task Copy(CaptureRecord record)
    {
        using var lease = Repository.Lease([record]); await CopyImage(ImageService.Load(Repository.PathFor(record)));
    }
    public async Task CopyFiles(IReadOnlyList<CaptureRecord> records)
    {
        ocr.Cancel();
        var ordered = TransferPayload.Ordered(Repository, records);
        using var lease = Repository.Lease(ordered, transfer: true);
        if (!await Clipboard.FilesAsync(TransferPayload.Files(Repository, ordered))) Notify("Clipboard is busy. Try Copy again.");
    }
    public async Task CopyPath(CaptureRecord record, bool filename = false)
    {
        using var lease = Repository.Lease([record], transfer: true);
        await CopyText(filename ? record.FileName : Repository.PathFor(record));
    }
    public async Task<bool> CopyText(string text, CancellationToken cancellation = default)
    {
        if (cancellation == default) ocr.Cancel();
        var success = await Clipboard.TextAsync(text, cancellation);
        if (!success && !cancellation.IsCancellationRequested) Notify("Text was not copied. Clipboard is busy or a newer copy superseded it; retry Copy.");
        return success;
    }
    public async Task<string> OcrText(BitmapSource image, CancellationToken lifetime = default)
    {
        var token = ocr.Begin(lifetime); Clipboard.Invalidate();
        try
        {
            var text = await OcrService.ReadAsync(ImageService.Png(image), token);
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(text)) return "No text found.";
            return await CopyText(text, token) ? "OCR text copied." : "OCR text was not copied. Retry OCR when the clipboard is available.";
        }
        catch (OperationCanceledException) { return "OCR cancelled."; }
        catch (Exception ex) { Log("failure_category=Ocr; exception_type=" + ex.GetType().Name); return ex is InvalidDataException ? "OCR input is invalid or too large. Use a smaller valid image; capture remains available." : "OCR failed. Check the local English model and complete x64 package/Visual C++ runtime, then retry OCR. Capture remains available."; }
    }
    public async Task Ocr(CaptureRecord record)
    {
        using var lease = Repository.Lease([record]);
        Notify(await OcrText(ImageService.Load(Repository.PathFor(record))));
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
            Failure(ex); return new(ExportStatus.Failed, Message: OperationFailure.From(ex).Message);
        }
    }
    public void OpenExportFolder(string path) => Try(() =>
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!Directory.Exists(directory)) throw new IOException("The export folder is no longer available.");
        Process.Start(new ProcessStartInfo { FileName = directory!, UseShellExecute = true });
    });
    public void Pin(CaptureRecord record) => Try(() => { Repository.SetPinned(record, !record.Pinned); Dock.Refresh(); });
    public void Detach(CaptureRecord record) => Try(() =>
    {
        Repository.SetPinned(record, true);
        if (pins.TryGetValue(record.Id, out var existing)) { existing.RestoreInteraction(); return; }
        var pin = new PinWindow(this, record); pins[record.Id] = pin; pin.Closed += (_, _) => pins.Remove(record.Id); pin.Show();
    });
    public void Dismiss(IEnumerable<CaptureRecord> records) => Try(() => { Repository.Dismiss(records); Dock.Refresh(); });
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
        ConfigureHotkeys(hotkeyPause.Enter(Hotkeys.Paused));
        welcomeWindow = new(this); welcomeWindow.Closed += (_, _) => { welcomeWindow = null; ConfigureHotkeys(hotkeyPause.Exit()); BuildTray(); }; welcomeWindow.Show();
    }
    public void FinishSetup() => welcomeWindow?.Close();
    public void ShowSettings() => ShowSettings(false);
    private void ShowSettings(bool welcome)
    {
        if (settingsWindow is not null) { settingsWindow.Activate(); return; }
        ConfigureHotkeys(hotkeyPause.Enter(Hotkeys.Paused));
        settingsWindow = new(this, welcome); settingsWindow.Closed += (_, _) => { settingsWindow = null; ConfigureHotkeys(hotkeyPause.Exit()); BuildTray(); }; settingsWindow.Show();
    }
    public void ShowHistory()
    {
        if (historyWindow is not null) { historyWindow.Activate(); return; }
        historyWindow = new(this); historyWindow.Closed += (_, _) => historyWindow = null; historyWindow.Show();
    }
    public void ApplySettings(Settings settings)
    {
        if (Diagnostic && settings.CachePath.Length > 0) throw new InvalidDataException("Isolated checks use their temporary cache only.");
        if (settings.DockMonitor < 0) settings.DockMonitorIdentity = "";
        else if (settings.DockMonitorIdentity.Length == 0 || (settings.DockMonitor != Settings.DockMonitor && settings.DockMonitorIdentity == Settings.DockMonitorIdentity))
            settings.DockMonitorIdentity = MonitorService.All().ElementAtOrDefault(settings.DockMonitor)?.Identity ?? "";
        if (Diagnostic) { settings.LaunchOnStartup = false; settingsService.Save(settings); }
        else StartupService.Apply(settings, settingsService.Save);
        Settings = settings;
        Repository.HistoryEnabled = settings.HistoryEnabled; Try(Repository.Persist);
        Ui.Theme(settings.Theme); ConfigureHotkeys(Hotkeys.Paused); cleanup.Interval = TimeSpan.FromMinutes(settings.CleanupMinutes); Dock.Refresh(); BuildTray();
        SettingsChanged?.Invoke();
        if (Hotkeys.Warnings.Count > 0) Notify(string.Join("\n", Hotkeys.Warnings));
    }
    internal void ConfigureHotkeys(bool paused) => Hotkeys.Configure(Settings, Diagnostic || paused);
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
        Item("Pause hotkeys", () => { ConfigureHotkeys(hotkeyPause.Toggle(Hotkeys.Paused)); BuildTray(); }, Hotkeys.Paused);
        Item("Clear temporary screenshots", ClearTemporary); Item("Retry history save", () => { Repository.Persist(); Notify("History saved. Cleanup can resume."); });
        if (!Diagnostic) Item("Launch at Windows login", () => { var draft = System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(Settings))!; draft.LaunchOnStartup = !StartupService.Enabled; ApplySettings(draft); }, StartupService.Enabled);
        Item("Last operation details", () => MessageBox.Show(lastNotice, "SnippyGrab · Operation details"));
        Item("About", () => MessageBox.Show("SnippyGrab " + BuildVersion.Display + "\nNative, local screenshot shelf. MIT licensed.\nNo uploads, accounts, analytics or update polling.\n\nPrint Screen: region · Ctrl+Shift+S: fallback\nCtrl-click: select several · Drag: attach files\nClick: edit · Alt-drag: reorder\n\nUnsigned build. See README for verification and limitations.", "SnippyGrab"));
        menu.Items.Add(new Forms.ToolStripSeparator()); Item("Exit", Exit);
        var old = tray.ContextMenuStrip; tray.ContextMenuStrip = menu; old?.Dispose();
    }
    private void DisplayChanged(object? sender, EventArgs e) => lifecycle.Post(() => Try(Dock.TopologyChanged));
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Resume) lifecycle.Post(() => Try(() => { ConfigureHotkeys(Hotkeys.Paused); Repository.Cleanup(DateTimeOffset.UtcNow, Settings.RetentionHours); Dock.TopologyChanged(); BuildTray(); if (Hotkeys.Warnings.Count > 0) Notify(string.Join("\n", Hotkeys.Warnings)); })); }
    private void PreferencesChanged(object sender, UserPreferenceChangedEventArgs e) => lifecycle.Post(() => Ui.Theme(Settings.Theme));
    public async void Run(Func<Task> action) { try { await action(); } catch (Exception ex) { Failure(ex); } }
    public void Try(Action action) { try { action(); } catch (Exception ex) { Failure(ex); } }
    public void Failure(Exception ex)
    {
        var failure = OperationFailure.From(ex);
        if (failure.Kind == FailureKind.Cancelled) return;
        Log("failure_category=" + failure.Kind + "; exception_type=" + ex.GetType().Name);
        Notify(failure.Message);
    }
    private void Log(string text)
    {
        try { var path = Path.Combine(dataDirectory, "diagnostics.log"); if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024) File.Delete(path); File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {text}\n"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    public void Notify(string message)
    {
        if (Exiting) return;
        var now = DateTimeOffset.UtcNow;
        if (message == lastNotice && now - lastNoticeUtc < TimeSpan.FromSeconds(5)) return;
        lastNotice = message; lastNoticeUtc = now;
        tray.ShowBalloonTip(3500, "SnippyGrab", message.Length > 250 ? message[..210] + "… Tray → Last operation details." : message, Forms.ToolTipIcon.Info);
    }
    public void Exit()
    {
        if (Exiting || exitRequested) return;
        Run(ExitAsync);
    }
    private async Task ExitAsync()
    {
        exitRequested = true;
        var wasPaused = Hotkeys.Paused;
        ConfigureHotkeys(true);
        try
        {
            foreach (var editor in editors.Values.ToArray())
                if (!await editor.RequestCloseAsync()) return;
            if (Settings.SessionOnly) { foreach (var pin in pins.Values.ToArray()) pin.Close(); Repository.CleanupSession(DateTimeOffset.UtcNow); }
            else Repository.Persist();
            Exiting = true;
            Application.Current.Shutdown();
        }
        finally
        {
            exitRequested = false;
            if (!Exiting) ConfigureHotkeys(wasPaused);
        }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        Exiting = true; lifecycle.Dispose(); cleanup.Stop(); expiry.Stop();
        SystemEvents.DisplaySettingsChanged -= DisplayChanged; SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.UserPreferenceChanged -= PreferencesChanged;
        Ui.FailureHandler = null; ocr.Dispose(); Clipboard.Invalidate(); Hotkeys.Dispose(); tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Icon?.Dispose(); tray.Dispose();
    }
}
