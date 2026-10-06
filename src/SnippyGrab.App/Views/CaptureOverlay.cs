using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using SnippyGrab.App.Services;

namespace SnippyGrab.App.Views;

internal sealed class CaptureOverlay : Window
{
    private readonly PixelRect desktop;
    private readonly SelectionSurface surface;
    private readonly bool pickWindow;
    private Native.POINT origin;
    private bool selecting;
    private readonly bool activate;
    public PixelRect? Selection { get; private set; }
    public Native.POINT? WindowPoint { get; private set; }
    public CaptureOverlay(BitmapSource frozen, PixelRect bounds, bool windowMode, bool activate = true)
    {
        desktop = bounds; pickWindow = windowMode; this.activate = activate;
        ShowActivated = activate;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        Topmost = true; Background = Brushes.Black; Cursor = Cursors.Cross;
        surface = new(frozen, bounds.Width, bounds.Height, windowMode);
        Content = new System.Windows.Controls.Viewbox { Stretch = Stretch.Fill, Child = surface };
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle).AddHook(KeepDesktopBounds);
            Native.SetWindowPos(handle, Topmost ? -1 : -2, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0010);
        };
        Loaded += (_, _) => { if (this.activate) { Activate(); Focus(); } };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        MouseLeftButtonDown += (_, e) =>
        {
            Native.GetCursorPos(out origin); selecting = true; CaptureMouse();
            if (pickWindow) { WindowPoint = origin; Close(); }
            e.Handled = true;
        };
        MouseMove += (_, _) =>
        {
            if (!selecting || pickWindow) return;
            Native.GetCursorPos(out var point);
            surface.Region = PixelRect.Between(origin.X, origin.Y, point.X, point.Y).Intersect(desktop).RelativeTo(desktop);
            surface.InvalidateVisual();
        };
        MouseLeftButtonUp += (_, _) =>
        {
            if (!selecting) return;
            Native.GetCursorPos(out var point);
            var region = PixelRect.Between(origin.X, origin.Y, point.X, point.Y).Intersect(desktop);
            if (region.Width >= 2 && region.Height >= 2) Selection = region;
            ReleaseMouseCapture(); Close();
        };
    }
    private nint KeepDesktopBounds(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0024) // WM_GETMINMAXINFO: a multi-monitor overlay must exceed monitor tracking limits.
        {
            var limits = Marshal.PtrToStructure<Native.MINMAXINFO>(lParam);
            limits.MaxTrackSize = new() { X = desktop.Width, Y = desktop.Height };
            limits.MaxSize = limits.MaxTrackSize;
            Marshal.StructureToPtr(limits, lParam, false);
        }
        else if (message == 0x0046) // WM_WINDOWPOSCHANGING: WPF startup/DPI layout cannot shrink or move the snapshot.
        {
            var position = Marshal.PtrToStructure<Native.WINDOWPOS>(lParam);
            position.X = desktop.X; position.Y = desktop.Y;
            position.Width = desktop.Width; position.Height = desktop.Height;
            position.Flags &= ~0x0003u; // SWP_NOSIZE | SWP_NOMOVE
            Marshal.StructureToPtr(position, lParam, false);
        }
        return 0;
    }
    private sealed class SelectionSurface(BitmapSource image, int width, int height, bool windowMode) : FrameworkElement
    {
        public PixelRect Region { get; set; }
        protected override Size MeasureOverride(Size availableSize) => new(width, height);
        protected override void OnRender(DrawingContext dc)
        {
            var bounds = new Rect(0, 0, width, height); dc.DrawImage(image, bounds);
            Geometry shade = new RectangleGeometry(bounds);
            if (!Region.IsEmpty)
                shade = Geometry.Combine(shade, new RectangleGeometry(new Rect(Region.X, Region.Y, Region.Width, Region.Height)), GeometryCombineMode.Exclude, null);
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(78, 0, 0, 0)), null, shade);
            if (!Region.IsEmpty)
            {
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(169, 206, 255)), 2), new Rect(Region.X, Region.Y, Region.Width, Region.Height));
                var text = new FormattedText($"{Region.Width} × {Region.Height}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 14, Brushes.White, 1);
                var x = Math.Clamp(Region.X, 4, width - text.Width - 20);
                var y = Region.Y > 36 ? Region.Y - 34 : Region.Bottom + 8;
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(220, 24, 29, 36)), null, new Rect(x, y, text.Width + 18, 28), 5, 5);
                dc.DrawText(text, new Point(x + 9, y + 4));
            }
            else
            {
                var text = new FormattedText(windowMode ? "Click a window · Esc to cancel" : "Drag to snip · Esc to cancel", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 16, Brushes.White, 1);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(225, 24, 29, 36)), null, new Rect((width - text.Width) / 2 - 14, 32, text.Width + 28, 42), 8, 8);
                dc.DrawText(text, new Point((width - text.Width) / 2, 43));
            }
        }
    }
}
