using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace SnippyGrab.App.Services;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly PixelRect Pixels => new(Left, Top, Right - Left, Bottom - Top);
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MONITORINFO
    { public int Size; public RECT Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] internal struct CURSORINFO { public int Size; public int Flags; public nint Cursor; public POINT Position; }
    [StructLayout(LayoutKind.Sequential)] internal struct ICONINFO { [MarshalAs(UnmanagedType.Bool)] public bool Icon; public int HotX, HotY; public nint Mask, Color; }
    internal delegate bool MonitorCallback(nint monitor, nint dc, ref RECT rect, nint data);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] internal static extern void keybd_event(byte key, byte scan, uint flags, nuint extra);
    [DllImport("user32.dll")] internal static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nuint extra);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint window, out RECT rect);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(POINT point);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] internal static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorCallback callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(nint monitor, ref MONITORINFO info);
    [DllImport("shcore.dll")] internal static extern int GetDpiForMonitor(nint monitor, int kind, out uint x, out uint y);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] internal static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint window, int attribute, out RECT rect, int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmFlush();
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool SetDefaultDllDirectories(uint flags);
    [DllImport("user32.dll")] internal static extern bool GetCursorInfo(ref CURSORINFO info);
    [DllImport("user32.dll")] internal static extern bool GetIconInfo(nint icon, out ICONINFO info);
    [DllImport("user32.dll")] internal static extern bool DrawIconEx(nint dc, int x, int y, nint icon, int width, int height, uint step, nint brush, uint flags);
    [DllImport("user32.dll")] internal static extern nint GetDC(nint window);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern bool BitBlt(nint destination, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint operation);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint value);
    internal static PixelRect Desktop => new(GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79));
    internal static void NoActivate(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        SetWindowLongPtr(hwnd, -20, GetWindowLongPtr(hwnd, -20) | 0x08000000 | 0x80);
    }
}

internal sealed record MonitorInfo(nint Handle, PixelRect Bounds, PixelRect Work, uint Dpi, int Index);
internal static class MonitorService
{
    public static IReadOnlyList<MonitorInfo> All()
    {
        var monitors = new List<MonitorInfo>();
        Native.EnumDisplayMonitors(0, 0, (nint monitor, nint dc, ref Native.RECT rect, nint data) =>
        {
            var info = new Native.MONITORINFO { Size = Marshal.SizeOf<Native.MONITORINFO>() };
            Native.GetMonitorInfo(monitor, ref info);
            Native.GetDpiForMonitor(monitor, 0, out var dpi, out _);
            monitors.Add(new(monitor, info.Monitor.Pixels, info.Work.Pixels, dpi == 0 ? 96 : dpi, monitors.Count));
            return true;
        }, 0);
        return monitors;
    }
    public static MonitorInfo ForPointer(int configured = -1)
    {
        var monitors = All();
        if (configured >= 0 && configured < monitors.Count) return monitors[configured];
        Native.GetCursorPos(out var p);
        return monitors.FirstOrDefault(m => p.X >= m.Bounds.X && p.X < m.Bounds.Right && p.Y >= m.Bounds.Y && p.Y < m.Bounds.Bottom) ?? monitors[0];
    }
}
