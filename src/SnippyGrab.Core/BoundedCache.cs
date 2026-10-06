namespace SnippyGrab.Core;

public sealed class BoundedCache<TKey, TValue> where TKey : notnull
{
    private readonly int capacity;
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> entries = [];
    private readonly LinkedList<(TKey Key, TValue Value)> recent = new();
    public BoundedCache(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1); this.capacity = capacity;
    }
    public int Count => entries.Count;
    public bool TryGetValue(TKey key, out TValue value)
    {
        if (!entries.TryGetValue(key, out var node)) { value = default!; return false; }
        recent.Remove(node); recent.AddFirst(node); value = node.Value.Value; return true;
    }
    public TValue GetOrAdd(TKey key, Func<TValue> create)
    {
        if (TryGetValue(key, out var found)) return found;
        var value = create();
        if (entries.Count == capacity) Remove(recent.Last!.Value.Key);
        entries.Add(key, recent.AddFirst((key, value))); return value;
    }
    public void Remove(TKey key)
    {
        if (entries.Remove(key, out var node)) recent.Remove(node);
    }
    public void RemoveWhere(Func<TKey, bool> predicate)
    {
        foreach (var key in entries.Keys.Where(predicate).ToArray()) Remove(key);
    }
}
