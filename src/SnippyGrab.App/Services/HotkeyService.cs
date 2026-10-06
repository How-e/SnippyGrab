using System.Windows.Interop;
using Microsoft.Win32;

namespace SnippyGrab.App.Services;

internal sealed class HotkeyService : IDisposable
{
    private readonly HwndSource source = new(new HwndSourceParameters("SnippyGrab.Hotkeys") { Width = 0, Height = 0, WindowStyle = 0 });
    private readonly Dictionary<int, CaptureMode> active = [];
    public List<string> Warnings { get; } = [];
    public bool Paused { get; private set; }
    public event Action<CaptureMode>? Capture;
    public HotkeyService() => source.AddHook(Hook);
    public void Configure(Settings settings, bool paused = false)
    {
        foreach (var id in active.Keys) Native.UnregisterHotKey(source.Handle, id);
        active.Clear(); Paused = paused;
        if (paused) return;
        Warnings.Clear();
        var requested = new[] { (settings.PrimaryHotkey, settings.DefaultCaptureMode), (settings.DesktopHotkey, CaptureMode.Desktop), (settings.WindowHotkey, CaptureMode.Window), (settings.ActiveWindowHotkey, CaptureMode.ActiveWindow), (settings.FallbackHotkey, CaptureMode.Region) };
        var idNext = 1;
        foreach (var (key, mode) in requested)
        {
            var id = idNext++;
            if (Native.RegisterHotKey(source.Handle, id, key.Modifiers | 0x4000, key.Key)) active[id] = mode;
            else Warnings.Add($"{key} is unavailable. Choose another hotkey in Settings.");
        }
        using var keyboard = Registry.CurrentUser.OpenSubKey(@"Control Panel\Keyboard");
        if (Convert.ToInt32(keyboard?.GetValue("PrintScreenKeyForSnippingEnabled", 0)) == 1)
            Warnings.Add("Windows uses Print Screen for screen snipping. In Settings → Accessibility → Keyboard, turn off ‘Use the Print Screen key to open screen capture’, then restart SnippyGrab. Ctrl+Shift+S is the fallback.");
    }
    private nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0312 && active.TryGetValue((int)wParam, out var mode)) { handled = true; Capture?.Invoke(mode); }
        return 0;
    }
    public void Dispose() { foreach (var id in active.Keys) Native.UnregisterHotKey(source.Handle, id); source.Dispose(); }
}
