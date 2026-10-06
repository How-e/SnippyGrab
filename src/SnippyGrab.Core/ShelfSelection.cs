namespace SnippyGrab.Core;

public enum ShelfAction { Copy, Dismiss, Edit, Export, Pin, History, Settings }

public sealed class ShelfSelection
{
    private IReadOnlyList<CaptureRecord> items = [];
    public HashSet<Guid> Selected { get; } = [];
    public Guid? Focused { get; private set; }
    public CaptureRecord? Current => items.FirstOrDefault(c => c.Id == Focused);
    public void Update(IReadOnlyList<CaptureRecord> visible, Guid? fallback = null)
    {
        items = visible;
        Selected.RemoveWhere(id => !items.Any(c => c.Id == id));
        if (!items.Any(c => c.Id == Focused)) Focused = items.FirstOrDefault(c => c.Id == fallback)?.Id ?? items.FirstOrDefault()?.Id;
    }
    public void Reset() { Selected.Clear(); Focused = null; }
    public void Focus(Guid id) { if (items.Any(c => c.Id == id)) Focused = id; }
    public void Toggle(Guid id) { Focus(id); if (Focused != id) return; if (!Selected.Add(id)) Selected.Remove(id); }
    public void Move(int delta)
    {
        if (items.Count == 0) return;
        var current = items.Select(c => c.Id).ToList().IndexOf(Focused ?? Guid.Empty);
        Focused = items[Math.Clamp(current + delta, 0, items.Count - 1)].Id;
    }
    public List<CaptureRecord> Targets(ShelfAction action) => action is ShelfAction.Copy or ShelfAction.Dismiss && Selected.Count > 0
        ? items.Where(c => Selected.Contains(c.Id)).ToList()
        : Current is { } current ? [current] : [];
}
