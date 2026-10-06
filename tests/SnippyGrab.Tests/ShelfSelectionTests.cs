using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class ShelfSelectionTests
{
    private static List<CaptureRecord> Items() => Enumerable.Range(0, 20).Select(_ => new CaptureRecord()).ToList();
    [Fact]
    public void SingleNonprimarySelectionDrivesCopyAndDelete()
    {
        var items = Items(); var selection = new ShelfSelection(); selection.Update(items);
        selection.Toggle(items[3].Id);
        Assert.Equal(items[3], Assert.Single(selection.Targets(ShelfAction.Copy)));
        Assert.Equal(items[3], Assert.Single(selection.Targets(ShelfAction.Dismiss)));
        Assert.Equal(items[3], Assert.Single(selection.Targets(ShelfAction.Edit)));
    }
    [Fact]
    public void NonadjacentSelectionsSurviveNavigationAndUseShelfOrder()
    {
        var items = Items(); var selection = new ShelfSelection(); selection.Update(items);
        selection.Toggle(items[17].Id); selection.Toggle(items[2].Id); selection.Focus(items[0].Id);
        Assert.Equal(new[] { items[2], items[17] }, selection.Targets(ShelfAction.Copy));
        Assert.Equal(new[] { items[2], items[17] }, selection.Targets(ShelfAction.Dismiss));
        selection.Move(19);
        Assert.Equal(items[19], Assert.Single(selection.Targets(ShelfAction.Edit)));
        Assert.Equal(items[19], Assert.Single(selection.Targets(ShelfAction.Export)));
        Assert.Equal(2, selection.Selected.Count);
    }
    [Fact]
    public void ReorderPreservesFocusedIdentityAndUpdatesSelectedPayloadOrder()
    {
        var items = Items(); var selection = new ShelfSelection(); selection.Update(items);
        selection.Toggle(items[0].Id); selection.Toggle(items[1].Id); var focused = selection.Focused;
        ShelfOrder.Move(items, items[0].Id, items[1].Id); selection.Update(items);
        Assert.Equal(focused, selection.Focused);
        Assert.Equal(items.Take(2), selection.Targets(ShelfAction.Copy));
    }
    [Fact]
    public void RemovedFocusAndSelectionsCannotTargetHiddenOrUnrelatedRecords()
    {
        var items = Items(); var selection = new ShelfSelection(); selection.Update(items);
        selection.Toggle(items[3].Id); selection.Update([items[5]], items[5].Id);
        Assert.Empty(selection.Selected); Assert.Equal(items[5], Assert.Single(selection.Targets(ShelfAction.Dismiss)));
        selection.Update([]); Assert.Empty(selection.Targets(ShelfAction.Copy)); Assert.Null(selection.Focused);
    }
}
