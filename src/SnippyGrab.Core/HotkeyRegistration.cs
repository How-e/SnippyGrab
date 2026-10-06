namespace SnippyGrab.Core;

public static class HotkeyRegistration
{
    public static Dictionary<int, CaptureMode> Register(Settings settings, Func<int, Hotkey, bool> register, ICollection<string> warnings)
    {
        var active = new Dictionary<int, CaptureMode>();
        var requested = new[] { (settings.PrimaryHotkey, settings.DefaultCaptureMode), (settings.DesktopHotkey, CaptureMode.Desktop), (settings.WindowHotkey, CaptureMode.Window), (settings.ActiveWindowHotkey, CaptureMode.ActiveWindow), (settings.FallbackHotkey, CaptureMode.Region) };
        for (var i = 0; i < requested.Length; i++)
        {
            var (key, mode) = requested[i];
            if (key.Key == 0) continue;
            if (register(i + 1, key with { Modifiers = key.Modifiers | 0x4000 })) active[i + 1] = mode;
            else warnings.Add($"{key} is unavailable. Choose another hotkey in Settings. Tray capture remains available.");
        }
        return active;
    }
    public static string Guidance(Settings settings) =>
        "Windows Settings → Accessibility → Keyboard: turn off ‘Use the Print Screen key to open screen capture’ if Windows intercepts Print Screen, then restart SnippyGrab. SnippyGrab never changes this Windows preference. " +
        $"Configured region fallback: {settings.FallbackHotkey}. Change it in SnippyGrab Settings if it conflicts; tray → Capture region always works without a hotkey.";
}
