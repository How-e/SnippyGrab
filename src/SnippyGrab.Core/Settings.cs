using System.Text.Json;

namespace SnippyGrab.Core;

public enum CaptureMode { Region, Desktop, Window, ActiveWindow, Monitor }
public enum DockCorner { BottomRight, BottomLeft, TopRight, TopLeft, Top, Bottom, Left, Right }
public enum DockOrientation { Vertical, Horizontal }
public enum AppTheme { System, Dark, Light }
public enum PreviewQuality { Balanced, Sharp, Original }
public enum OcrLayout { Auto, SparseText, SingleBlock }
public sealed record Hotkey(uint Key, uint Modifiers)
{
    private string KeyName => Key == 44 ? "PrintScreen" : Key is >= 112 and <= 135 ? "F" + (Key - 111) : Key is >= 65 and <= 90 or >= 48 and <= 57 ? ((char)Key).ToString() : "VK " + Key;
    public override string ToString() => Key == 0 ? "Disabled" : $"{((Modifiers & 2) != 0 ? "Ctrl+" : "")}{((Modifiers & 1) != 0 ? "Alt+" : "")}{((Modifiers & 4) != 0 ? "Shift+" : "")}{((Modifiers & 8) != 0 ? "Win+" : "")}{KeyName}";
}

public sealed class Settings
{
    public int SchemaVersion { get; set; } = 1;
    public bool FirstRunComplete { get; set; }
    public CaptureMode DefaultCaptureMode { get; set; } = CaptureMode.Region;
    public Hotkey PrimaryHotkey { get; set; } = new(44, 0);
    public Hotkey DesktopHotkey { get; set; } = new(44, 2);
    public Hotkey WindowHotkey { get; set; } = new(44, 3);
    public Hotkey ActiveWindowHotkey { get; set; } = new(44, 1);
    public Hotkey FallbackHotkey { get; set; } = new(83, 6);
    public Hotkey MonitorHotkey { get; set; } = new(0, 0);
    public bool IncludeCursor { get; set; }
    public bool Animate { get; set; } = true;
    public int DockMonitor { get; set; } = -1;
    public string DockMonitorIdentity { get; set; } = "";
    public DockCorner Corner { get; set; } = DockCorner.BottomRight;
    public DockOrientation Orientation { get; set; }
    public int ThumbnailSize { get; set; } = 224;
    public PreviewQuality PreviewQuality { get; set; } = PreviewQuality.Sharp;
    public bool OcrEnhanceSmallText { get; set; } = true;
    public OcrLayout OcrLayout { get; set; } = OcrLayout.Auto;
    public int ExpandedItems { get; set; } = 3;
    public double DockOpacity { get; set; } = 0.96;
    public bool AlwaysOnTop { get; set; } = true;
    public bool AutoCollapse { get; set; } = true;
    public int AutoHideSeconds { get; set; }
    public int DockLifetimeMinutes { get; set; } = 30;
    public bool AutoCopy { get; set; } = true;
    public bool ClipboardPng { get; set; } = true;
    public string CachePath { get; set; } = "";
    public int CleanupMinutes { get; set; } = 10;
    public int RetentionHours { get; set; } = 24;
    public bool SessionOnly { get; set; }
    public string SaveDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    public string AnnotationColor { get; set; } = "#FFEF675E";
    public double StrokeSize { get; set; } = 3;
    public double TextSize { get; set; } = 24;
    public bool HistoryEnabled { get; set; } = true;
    public bool LaunchOnStartup { get; set; }
    public bool StartMinimized { get; set; } = true;
    public AppTheme Theme { get; set; } = AppTheme.System;
    // Runtime networking is deliberately absent. Updates are manual, via signed/checksummed releases.
    public void Validate()
    {
        if (SchemaVersion > 1 || SchemaVersion < 0) throw new InvalidDataException("Unsupported settings version.");
        SchemaVersion = 1;
        CachePath ??= "";
        DockMonitorIdentity ??= "";
        SaveDirectory ??= Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        AnnotationColor ??= "#FFEF675E";
        ThumbnailSize = Math.Clamp(ThumbnailSize, 120, 400);
        ExpandedItems = Math.Clamp(ExpandedItems, 1, 5);
        DockOpacity = double.IsFinite(DockOpacity) ? Math.Clamp(DockOpacity, 0.25, 1) : 0.96;
        CleanupMinutes = Math.Clamp(CleanupMinutes, 1, 1440);
        RetentionHours = RetentionHours < 0 ? -1 : Math.Clamp(RetentionHours, 1, 8760);
        DockLifetimeMinutes = Math.Clamp(DockLifetimeMinutes, 0, 10080);
        AutoHideSeconds = Math.Clamp(AutoHideSeconds, 0, 86400);
        StrokeSize = double.IsFinite(StrokeSize) ? Math.Clamp(StrokeSize, 1, 30) : 3;
        TextSize = double.IsFinite(TextSize) ? Math.Clamp(TextSize, 8, 120) : 24;
        if (!Enum.IsDefined(Corner)) Corner = DockCorner.BottomRight;
        if (!Enum.IsDefined(Orientation)) Orientation = DockOrientation.Vertical;
        if (!Enum.IsDefined(Theme)) Theme = AppTheme.System;
        if (!Enum.IsDefined(PreviewQuality)) PreviewQuality = PreviewQuality.Sharp;
        if (!Enum.IsDefined(OcrLayout)) OcrLayout = OcrLayout.Auto;
        if (!Enum.IsDefined(DefaultCaptureMode)) DefaultCaptureMode = CaptureMode.Region;
        foreach (var key in new[] { PrimaryHotkey, DesktopHotkey, WindowHotkey, ActiveWindowHotkey, FallbackHotkey, MonitorHotkey })
            if (key is null || key.Key > 254 || key.Modifiers > 15 || (key.Key == 0 && key.Modifiers != 0)) throw new InvalidDataException("Invalid hotkey.");
    }
}

public sealed class SettingsService(string file)
{
    public bool Recovered { get; private set; }
    public Settings Load()
    {
        ManagedPath.RejectRedirects(file);
        if (!File.Exists(file)) return new();
        try
        {
            if (new FileInfo(file).Length > 128 * 1024) throw new InvalidDataException("Settings file is too large.");
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(file)) ?? new();
            settings.Validate();
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Recovered = true;
            try { File.Move(file, file + ".invalid-" + Guid.NewGuid().ToString("N")); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return new();
        }
    }
    public void Save(Settings settings)
    {
        settings.Validate();
        AtomicFile.Write(file, JsonSerializer.SerializeToUtf8Bytes(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public static class AtomicFile
{
    public static void Write(string path, byte[] data) => Write(path, data, true);
    public static void Write(string path, byte[] data, bool overwrite)
    {
        ManagedPath.RejectRedirects(path);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(data); stream.Flush(true); }
            ManagedPath.RejectRedirects(path);
            File.Move(temporary, path, overwrite);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
