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
        foreach (var entry in HotkeyRegistration.Register(settings, (id, key) => Native.RegisterHotKey(source.Handle, id, key.Modifiers, key.Key), Warnings))
            active[entry.Key] = entry.Value;
        using var keyboard = Registry.CurrentUser.OpenSubKey(@"Control Panel\Keyboard");
        if (Convert.ToInt32(keyboard?.GetValue("PrintScreenKeyForSnippingEnabled", 0)) == 1)
            Warnings.Add(HotkeyRegistration.Guidance(settings));
    }
    private nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0312 && active.TryGetValue((int)wParam, out var mode)) { handled = true; Capture?.Invoke(mode); }
        return 0;
    }
    public void Dispose() { foreach (var id in active.Keys) Native.UnregisterHotKey(source.Handle, id); source.Dispose(); }
}
