namespace SnippyGrab.App.Services;

internal static class CrashDiagnostics
{
    private static readonly object Gate = new();
    internal static void Record(string category, Exception? error)
    {
        // Fatal handlers cannot safely dispatch WPF teardown or wait for worker locks.
        // Keep a bounded, content-free diagnostic and let the runtime terminate.
        try
        {
            if (!Monitor.TryEnter(Gate)) return;
            try
            {
                var root = ManagedPath.StorageRoot(Path.Combine(ManagedPath.ResolveDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)), "SnippyGrab"));
                var path = Path.Combine(root, "crash-events.log");
                ManagedPath.RejectRedirects(path); Directory.CreateDirectory(root);
                if (File.Exists(path) && new FileInfo(path).Length > 128 * 1024) File.Delete(path);
                File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} category={category}; exception_type={error?.GetType().FullName ?? "Unknown"}\n");
            }
            finally { Monitor.Exit(Gate); }
        }
        catch { } // Diagnostics must never throw from a runtime exception callback.
    }
}
