namespace SnippyGrab.Core;

public static class MonitorIdentity
{
    public static int ConfiguredIndex(IReadOnlyList<string> identities, int configured, string saved, int primary)
    {
        if (configured < 0) return -1;
        if (saved.Length == 0) return configured < identities.Count ? configured : primary;
        for (var i = 0; i < identities.Count; i++) if (identities[i] == saved) return i;
        return primary; // Retain the saved identity so reconnect restores the intended display.
    }
}
