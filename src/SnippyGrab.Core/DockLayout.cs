namespace SnippyGrab.Core;

public static class DockLayout
{
    public static DockOrientation Orientation(DockOrientation configured, DockCorner corner) => corner switch
    {
        DockCorner.Top or DockCorner.Bottom => DockOrientation.Vertical,
        DockCorner.Left or DockCorner.Right => DockOrientation.Horizontal,
        _ => configured
    };
    public static bool Right(DockCorner corner) => corner is DockCorner.TopRight or DockCorner.BottomRight or DockCorner.Right;
    public static bool Bottom(DockCorner corner) => corner is DockCorner.BottomLeft or DockCorner.BottomRight or DockCorner.Bottom;
    public static bool Reverse(DockOrientation orientation, DockCorner corner) => orientation == DockOrientation.Vertical
        ? Bottom(corner)
        : Right(corner);

    public static PixelRect Place(PixelRect work, int width, int height, DockCorner corner, int inset = 16)
    {
        width = Math.Clamp(width, 1, work.Width); height = Math.Clamp(height, 1, work.Height);
        var right = Right(corner);
        var bottom = Bottom(corner);
        var x = right ? work.Right - width - inset : work.X + inset;
        var y = bottom ? work.Bottom - height - inset : work.Y + inset;
        if (corner is DockCorner.Top or DockCorner.Bottom) x = work.X + (work.Width - width) / 2;
        if (corner is DockCorner.Left or DockCorner.Right) y = work.Y + (work.Height - height) / 2;
        return new(Math.Clamp(x, work.X, work.Right - width), Math.Clamp(y, work.Y, work.Bottom - height), width, height);
    }
}

public static class ShelfOrder
{
    // Dropping onto a card takes that card's original position, symmetrically in either direction.
    public static bool Move(List<CaptureRecord> captures, Guid from, Guid target)
    {
        var source = captures.FindIndex(c => c.Id == from); var destination = captures.FindIndex(c => c.Id == target);
        if (source < 0 || destination < 0 || source == destination) return false;
        var record = captures[source]; captures.RemoveAt(source); captures.Insert(destination, record); return true;
    }
}
