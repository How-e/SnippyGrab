namespace SnippyGrab.Core;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public static PixelRect Between(int x1, int y1, int x2, int y2) => new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
    public PixelRect Intersect(PixelRect other)
    {
        int left = Math.Max(X, other.X), top = Math.Max(Y, other.Y);
        return new(left, top, Math.Max(0, Math.Min(Right, other.Right) - left), Math.Max(0, Math.Min(Bottom, other.Bottom) - top));
    }
    public PixelRect RelativeTo(PixelRect desktop) => new(X - desktop.X, Y - desktop.Y, Width, Height);
}
public static class DpiGeometry
{
    public static double ToDip(double pixels, double dpi) => pixels * 96 / dpi;
    public static int ToPixel(double dips, double dpi) => (int)Math.Round(dips * dpi / 96);
}
public static class CaptureSelection
{
    public static PixelRect? Region(int startX, int startY, int endX, int endY, PixelRect desktop)
    {
        var area = PixelRect.Between(startX, startY, endX, endY).Intersect(desktop);
        return area.Width >= 2 && area.Height >= 2 ? area : null;
    }
    public static (int X, int Y) CursorOrigin(int x, int y, int hotspotX, int hotspotY, PixelRect capture) => (x - capture.X - hotspotX, y - capture.Y - hotspotY);
}

public sealed class UndoJournal<T>(T initial, int capacity = 60, Func<IReadOnlyList<T>, long>? retainedBytes = null, long byteBudget = long.MaxValue)
{
    private readonly List<T> states = [initial];
    private int index;
    public T Current => states[index];
    public bool CanUndo => index > 0;
    public bool CanRedo => index + 1 < states.Count;
    public void Push(T state)
    {
        states.RemoveRange(index + 1, states.Count - index - 1);
        states.Add(state);
        while (states.Count > 1 && (states.Count > capacity || (retainedBytes?.Invoke(states) ?? 0) > byteBudget)) states.RemoveAt(0);
        index = states.Count - 1;
    }
    public T Undo() { if (CanUndo) index--; return Current; }
    public T Redo() { if (CanRedo) index++; return Current; }
    public void Clear() { states.Clear(); index = 0; }
}
