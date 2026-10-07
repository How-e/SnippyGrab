namespace SnippyGrab.Core;

public sealed record PinLayout(string MonitorIdentity, double X, double Y, double Width, double Height, double Opacity, bool Topmost, bool ClickThrough)
{
    public PinLayout Normalize() => this with
    {
        MonitorIdentity = (MonitorIdentity ?? "")[..Math.Min((MonitorIdentity ?? "").Length, 512)],
        X = double.IsFinite(X) ? Math.Clamp(X, -100000, 100000) : 0,
        Y = double.IsFinite(Y) ? Math.Clamp(Y, -100000, 100000) : 0,
        Width = double.IsFinite(Width) ? Math.Clamp(Width, 80, 4096) : 320,
        Height = double.IsFinite(Height) ? Math.Clamp(Height, 60, 4096) : 220,
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, 0.25, 1) : 1
    };
    public PixelRect Place(PixelRect work, double dpi)
    {
        var layout = Normalize();
        if (!double.IsFinite(dpi) || dpi <= 0 || work.IsEmpty) throw new ArgumentOutOfRangeException(nameof(dpi));
        var width = Math.Min(work.Width, DpiGeometry.ToPixel(layout.Width, dpi));
        var height = Math.Min(work.Height, DpiGeometry.ToPixel(layout.Height, dpi));
        return new(Math.Clamp(work.X + DpiGeometry.ToPixel(layout.X, dpi), work.X, work.Right - width),
            Math.Clamp(work.Y + DpiGeometry.ToPixel(layout.Y, dpi), work.Y, work.Bottom - height), width, height);
    }
}
