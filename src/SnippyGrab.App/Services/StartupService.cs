using Microsoft.Win32;

namespace SnippyGrab.App.Services;

internal static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled
    {
        get { return StartupCommand.Matches(Command, Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable.")); }
    }
    public static string? Command { get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue("SnippyGrab") as string; } }
    public static bool Stale => Command is not null && !Enabled;
    public static void Apply(Settings settings, Action<Settings> save) => StartupSettingsTransaction.Apply(settings, Command,
        Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable."), command =>
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (command is null) key.DeleteValue("SnippyGrab", false); else key.SetValue("SnippyGrab", command);
        }, save);
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable.");
            if (!File.Exists(executable)) throw new FileNotFoundException("Startup executable is unavailable.");
            key.SetValue("SnippyGrab", StartupCommand.Build(executable));
        }
        else key.DeleteValue("SnippyGrab", false);
    }
}
