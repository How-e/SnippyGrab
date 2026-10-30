using System.Text.Json;

namespace SnippyGrab.Core;

public sealed record DesignChange(string Id, string LayerId, string Name, string Operation, string Property, JsonElement Before, JsonElement After, string Scope, string Intent, DesignEvidence Source);

public sealed class DesignSession
{
    public DesignDocument Document { get; }
    public bool CanUndo => Document.Head > 0;
    public bool CanRedo => Document.Head < Document.History.Count;
    public DesignSession(DesignDocument document) { document.Validate(); Document = document; }
    public void Apply(string name, Action<List<DesignLayer>> edit)
    {
        var next = DesignDocument.Copy(Document.Layers); edit(next);
        DesignDocument.Normalize(next);
        Document.ValidateLayers(next);
        var changes = Changes(Document.Layers, next);
        if (changes.Count == 0) return;
        var transaction = new DesignTransaction(name, DateTimeOffset.UtcNow, changes) { BaseRevision = Document.Revision };
        var history = Document.History.Take(Document.Head).Append(transaction).TakeLast(200).ToList();
        if (JsonSerializer.SerializeToUtf8Bytes(history, DesignDocument.Json).Length > 32 * 1024 * 1024) throw new InvalidDataException("Undo history exceeds 32 MiB; save a new design snapshot before more edits.");
        foreach (var n in next)
        {
            if (n.Private && !Document.PrivateIds.Contains(n.Id)) Document.PrivateIds.Add(n.Id);
            if (!n.Private && Document.Layers.Any(old => old.Id == n.Id && old.Private)) Document.PrivateIds.Remove(n.Id);
        }
        Document.Layers = next; Document.History = history; Document.Head = history.Count; Document.Revision++;
    }
    public void Undo() { if (!CanUndo) return; var next = ApplyTransaction(Document.Layers, Document.History[Document.Head - 1], false); UpdatePrivacy(next); Document.Layers = next; Document.Head--; Document.Revision++; }
    public void Redo() { if (!CanRedo) return; var next = ApplyTransaction(Document.Layers, Document.History[Document.Head], true); UpdatePrivacy(next); Document.Layers = next; Document.Head++; Document.Revision++; }
    private void UpdatePrivacy(List<DesignLayer> next)
    {
        foreach (var n in next) { if (n.Private && !Document.PrivateIds.Contains(n.Id)) Document.PrivateIds.Add(n.Id); if (!n.Private && Document.Layers.Any(old => old.Id == n.Id && old.Private)) Document.PrivateIds.Remove(n.Id); }
    }
    private List<DesignLayer> ApplyTransaction(List<DesignLayer> source, DesignTransaction transaction, bool forward)
    {
        var next = DesignDocument.Copy(source);
        foreach (var c in transaction.Changes) next.RemoveAll(n => n.Id == c.Id);
        foreach (var c in transaction.Changes.OrderBy(c => forward ? c.AfterIndex : c.BeforeIndex))
        {
            var node = forward ? c.After : c.Before; var index = forward ? c.AfterIndex : c.BeforeIndex;
            if (node is not null) next.Insert(Math.Clamp(index, 0, next.Count), DesignDocument.Copy(node));
        }
        Document.ValidateLayers(next); return next;
    }
    public void Delete(IEnumerable<string> ids) => Apply("Delete layers", layers => { var all = Descendants(layers, ids); if (layers.Any(n => all.Contains(n.Id) && n.Locked)) throw new InvalidOperationException("Unlock the selected layers first."); layers.RemoveAll(n => all.Contains(n.Id)); });
    public void Duplicate(IEnumerable<string> ids) => Apply("Duplicate layers", layers =>
    {
        var all = Descendants(layers, ids); var map = all.ToDictionary(id => id, _ => Guid.NewGuid().ToString("N"));
        var copies = layers.Where(n => all.Contains(n.Id)).Select(DesignDocument.Copy).ToList();
        foreach (var n in copies) { var old = n.Id; n.Id = map[old]; n.Parent = n.Parent is { } p && map.TryGetValue(p, out var mapped) ? mapped : n.Parent; if (n.Parent is null || !map.ContainsValue(n.Parent)) { n.X += 16; n.Y += 16; } n.Name += " copy"; n.Locked = false; }
        layers.AddRange(copies);
    });
    public void Group(IEnumerable<string> ids) => Apply("Group layers", layers =>
    {
        var selected = layers.Where(n => ids.Contains(n.Id)).ToList();
        if (selected.Count < 2 || selected.Any(n => n.Locked) || selected.Select(n => n.Parent).Distinct().Count() != 1) throw new InvalidOperationException("Choose at least two unlocked sibling layers.");
        var x = selected.Min(n => n.X); var y = selected.Min(n => n.Y);
        var group = new DesignLayer { Kind = "group", Name = "Group", Parent = selected[0].Parent, X = x, Y = y, Width = selected.Max(n => n.X + n.Width) - x, Height = selected.Max(n => n.Y + n.Height) - y, Source = new() { Status = "unmapped" } };
        var index = layers.IndexOf(selected[0]); layers.Insert(index, group);
        foreach (var n in selected) { n.Parent = group.Id; n.X -= x; n.Y -= y; }
    });
    public void Ungroup(string id) => Apply("Ungroup layers", layers =>
    {
        var group = layers.Single(n => n.Id == id);
        if (group.Kind != "group" || group.Rotation != 0 || group.Locked) throw new InvalidOperationException("Choose an unlocked, unrotated group.");
        foreach (var n in layers.Where(n => n.Parent == id)) { n.Parent = group.Parent; n.X += group.X; n.Y += group.Y; }
        layers.Remove(group);
    });
    public void Reparent(string id, string? parent) => Apply("Move layer to container", layers =>
    {
        var node = layers.Single(n => n.Id == id); if (node.Locked || parent is not null && Descendants(layers, [id]).Contains(parent)) throw new InvalidOperationException("Choose an unlocked layer and a container outside its subtree.");
        var target = parent is null ? null : layers.Single(n => n.Id == parent);
        if (target is not null && target.Kind is not ("frame" or "group")) throw new InvalidOperationException("Only frames and groups can contain layers.");
        (double X, double Y) Origin(DesignLayer? n) { double x = 0, y = 0; while (n is not null) { if (n.Rotation != 0 || n.Locked) throw new InvalidOperationException("Unlock and clear rotation on the affected containers before reparenting."); x += n.X; y += n.Y; n = n.Parent is { } p ? layers.Single(v => v.Id == p) : null; } return (x, y); }
        var old = Origin(node); var next = Origin(target); node.X = old.X - next.X; node.Y = old.Y - next.Y; node.Parent = parent; layers.Remove(node); layers.Add(node);
    });
    public void Revert(string id) => Apply("Revert layer", layers =>
    {
        var baseline = Document.Baseline.SingleOrDefault(n => n.Id == id);
        if (baseline is null) { var all = Descendants(layers, [id]); layers.RemoveAll(n => all.Contains(n.Id)); return; }
        if (baseline.Parent is not null && !layers.Any(n => n.Id == baseline.Parent)) throw new InvalidOperationException("Restore the deleted parent first.");
        var index = layers.FindIndex(n => n.Id == id); if (index >= 0) layers[index] = DesignDocument.Copy(baseline); else layers.Insert(Math.Min(Document.Baseline.IndexOf(baseline), layers.Count), DesignDocument.Copy(baseline));
        foreach (var child in Document.Baseline.Where(n => Descendants(Document.Baseline, [id]).Contains(n.Id) && n.Id != id))
            if (!layers.Any(n => n.Id == child.Id)) layers.Add(DesignDocument.Copy(child));
    });
    public IReadOnlyList<DesignChange> NetChanges(List<DesignLayer>? state = null)
    {
        var current = state ?? Document.Layers; var result = new List<DesignChange>();
        var baseline = Document.Baseline.ToDictionary(n => n.Id); var edited = current.ToDictionary(n => n.Id);
        var privateIds = Document.PrivateIds.Concat(current.Where(n => n.Private).Select(n => n.Id)).ToArray();
        var privateAll = Descendants(Document.Baseline, privateIds).Union(Descendants(current, privateIds)).ToHashSet();
        var oldOrders = Document.Baseline.Where(n => edited.ContainsKey(n.Id)).GroupBy(n => n.Parent ?? "").ToDictionary(g => g.Key, g => g.Select(n => n.Id).ToArray());
        var newOrders = current.Where(n => baseline.ContainsKey(n.Id)).GroupBy(n => n.Parent ?? "").ToDictionary(g => g.Key, g => g.Select(n => n.Id).ToArray());
        foreach (var c in Changes(Document.Baseline, current))
        {
            var target = c.After ?? c.Before!;
            if (privateAll.Contains(target.Id)) continue;
            if (c.After is null && c.Before?.Parent is { } p && baseline.ContainsKey(p) && !edited.ContainsKey(p)) continue;
            if (c.Before is null && c.After?.Parent is { } added && !baseline.ContainsKey(added)) continue;
            var before = JsonSerializer.SerializeToElement(c.Before, DesignDocument.Json); var after = JsonSerializer.SerializeToElement(c.After, DesignDocument.Json);
            if (c.Before is null || c.After is null)
            {
                var subtree = c.After is null ? Document.Baseline : current; var ids = Descendants(subtree, [target.Id]);
                var value = JsonSerializer.SerializeToElement(subtree.Where(n => ids.Contains(n.Id)), DesignDocument.Json);
                result.Add(new(target.Id + "-" + (c.After is null ? "delete" : "insert"), target.Id, target.Name, c.After is null ? "deleteSubtree" : "insert", "layer", c.After is null ? value : before, c.Before is null ? value : after, target.Scope, target.Intent, target.Source)); continue;
            }
            foreach (var property in after.EnumerateObject())
            {
                if (property.Name is "id" or "source" or "private" or "locked" or "name" or "scope") continue;
                var b = before.GetProperty(property.Name); if (b.GetRawText() == property.Value.GetRawText()) continue;
                result.Add(new(target.Id + "-" + property.Name, target.Id, target.Name, property.Name == "text" ? "setText" : property.Name == "parent" ? "reparent" : "setProperty", property.Name, b, property.Value, target.Scope, target.Intent, target.Source));
            }
            var oldSiblings = oldOrders.GetValueOrDefault(target.Parent ?? "") ?? [];
            var newSiblings = newOrders.GetValueOrDefault(target.Parent ?? "") ?? [];
            if (c.Before.Parent == target.Parent && Array.IndexOf(oldSiblings, target.Id) != Array.IndexOf(newSiblings, target.Id)) result.Add(new(target.Id + "-order", target.Id, target.Name, "reorder", "order", JsonSerializer.SerializeToElement(Array.IndexOf(oldSiblings, target.Id)), JsonSerializer.SerializeToElement(Array.IndexOf(newSiblings, target.Id)), target.Scope, target.Intent, target.Source));
        }
        return result.OrderBy(c => c.LayerId, StringComparer.Ordinal).ThenBy(c => c.Property, StringComparer.Ordinal).ToList();
    }
    public static HashSet<string> Descendants(List<DesignLayer> layers, IEnumerable<string> ids)
    {
        var all = ids.ToHashSet(StringComparer.Ordinal); var children = layers.ToLookup(n => n.Parent ?? ""); var queue = new Queue<string>(all);
        while (queue.TryDequeue(out var id)) foreach (var child in children[id]) if (all.Add(child.Id)) queue.Enqueue(child.Id);
        return all;
    }
    public static bool IsPrivate(DesignLayer? node, List<DesignLayer> layers)
    {
        var seen = new HashSet<string>(); while (node is not null && seen.Add(node.Id)) { if (node.Private) return true; node = node.Parent is { } p ? layers.FirstOrDefault(n => n.Id == p) : null; }
        return false;
    }
    private static List<DesignLayerChange> Changes(List<DesignLayer> before, List<DesignLayer> after)
    {
        var b = before.Select((n, i) => (n, i)).ToDictionary(x => x.n.Id); var a = after.Select((n, i) => (n, i)).ToDictionary(x => x.n.Id); var result = new List<DesignLayerChange>();
        foreach (var id in b.Keys.Union(a.Keys))
        {
            var hasB = b.TryGetValue(id, out var old); var hasA = a.TryGetValue(id, out var next);
            if (hasB && hasA && old.i == next.i && JsonSerializer.Serialize(old.n, DesignDocument.Json) == JsonSerializer.Serialize(next.n, DesignDocument.Json)) continue;
            result.Add(new(id, hasB ? old.n : null, hasA ? next.n : null, hasB ? old.i : -1, hasA ? next.i : -1));
        }
        return result;
    }
}
