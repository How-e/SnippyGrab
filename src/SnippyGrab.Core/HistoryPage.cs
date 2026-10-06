namespace SnippyGrab.Core;

public static class HistoryPage
{
    public const int Size = 200;
    public static CaptureRecord[] Read(IEnumerable<CaptureRecord> captures, int page) =>
        captures.OrderByDescending(c => c.CreatedUtc).ThenBy(c => c.Id).Skip(Math.Max(0, page) * Size).Take(Size).ToArray();
}
