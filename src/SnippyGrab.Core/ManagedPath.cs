namespace SnippyGrab.Core;

public static class ManagedPath
{
    // Recheck on every operation: a validated root can be replaced after startup.
    public static void RejectRedirects(string path)
    {
        var full = Path.GetFullPath(path);
        for (var current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Managed storage cannot use redirected files or directories.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
}
