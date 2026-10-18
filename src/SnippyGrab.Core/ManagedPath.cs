using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SnippyGrab.Core;

public static class ManagedPath
{
    // Trust existing profile ancestors once, then operate on the physical path.
    // The owned root and its immediate parent may never themselves be redirects.
    public static string StorageRoot(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        RejectRedirect(full);
        var parent = Path.GetDirectoryName(full);
        if (parent is null) return full;
        RejectRedirect(parent);
        return Path.Combine(ResolveDirectory(parent), Path.GetFileName(full));
    }
    public static string ResolveDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        var missing = new Stack<string>();
        while (!Directory.Exists(full))
        {
            missing.Push(Path.GetFileName(full));
            full = Path.GetDirectoryName(full) ?? throw new DirectoryNotFoundException("Destination directory is unavailable.");
        }
        if (OperatingSystem.IsWindows())
        {
            using var handle = CreateFile(full, 0, 7, 0, 3, 0x02000000, 0);
            if (handle.IsInvalid) throw new IOException("Could not resolve storage directory.", new Win32Exception(Marshal.GetLastWin32Error()));
            var buffer = new StringBuilder(512);
            var length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
            if (length >= buffer.Capacity) { buffer.Capacity = checked((int)length + 1); length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0); }
            if (length == 0 || length >= buffer.Capacity) throw new IOException("Could not resolve storage directory.", new Win32Exception(Marshal.GetLastWin32Error()));
            full = buffer.ToString();
            if (full.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) full = @"\\" + full[8..];
            else if (full.StartsWith(@"\\?\", StringComparison.Ordinal)) full = full[4..];
        }
        else full = new DirectoryInfo(full).ResolveLinkTarget(true)?.FullName ?? full;
        foreach (var part in missing) full = Path.Combine(full, part);
        return Path.TrimEndingDirectorySeparator(full);
    }
    private static void RejectRedirect(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Managed storage cannot use redirected files or directories.");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, nint security, uint creation, uint flags, nint template);
    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint length, uint flags);

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
