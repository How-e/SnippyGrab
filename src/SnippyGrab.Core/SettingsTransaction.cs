namespace SnippyGrab.Core;

public static class SettingsTransaction
{
    public static void Apply(Settings settings, bool previousStartup, Action<bool> startup, Action<Settings> save)
    {
        settings.Validate();
        try { startup(settings.LaunchOnStartup); save(settings); }
        catch (Exception failure)
        {
            try { startup(previousStartup); }
            catch (Exception rollback) { throw new AggregateException("Settings were not saved; Windows startup rollback also failed. Review startup registration and retry Settings.", failure, rollback); }
            throw;
        }
    }
}
