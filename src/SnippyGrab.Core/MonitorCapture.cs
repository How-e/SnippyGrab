namespace SnippyGrab.Core;

public sealed record CaptureTarget(string MonitorIdentity);
public static class MonitorCapture
{
    public static PixelRect Resolve(IEnumerable<(string Identity, PixelRect Bounds)> monitors, CaptureTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.MonitorIdentity)) throw new InvalidOperationException("Choose a display before capturing.");
        var found = monitors.Where(m => m.Identity == target.MonitorIdentity).ToArray();
        if (found.Length != 1) throw new InvalidOperationException("The selected display is unavailable or ambiguous. Choose a display again.");
        var bounds = found[0].Bounds;
        if (bounds.IsEmpty || (long)bounds.Width * bounds.Height > 80_000_000) throw new InvalidDataException("Display capture exceeds the 80 megapixel limit or has invalid bounds.");
        return bounds;
    }
}
