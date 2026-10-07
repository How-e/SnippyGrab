namespace SnippyGrab.Core;

public static class StartupSettingsTransaction
{
    public static void Apply(Settings settings, string? previousCommand, string executable, Action<string?> write, Action<Settings> save)
    {
        settings.Validate();
        var desired = settings.LaunchOnStartup ? StartupCommand.Build(executable) : null;
        var changed = !string.Equals(previousCommand, desired, StringComparison.OrdinalIgnoreCase);
        try { if (changed) write(desired); save(settings); }
        catch (Exception failure)
        {
            try { if (changed) write(previousCommand); }
            catch (Exception rollback) { throw new AggregateException("Settings and startup rollback failed; review Windows startup before retrying.", failure, rollback); }
            throw;
        }
    }
}
