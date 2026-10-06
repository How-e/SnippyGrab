using Microsoft.Win32;

namespace SnippyGrab.App.Services;

internal static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue("SnippyGrab") is string; }
    }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue("SnippyGrab", StartupCommand.Build(Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable.")));
        else key.DeleteValue("SnippyGrab", false);
    }
}
