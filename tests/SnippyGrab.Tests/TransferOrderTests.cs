using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class TransferOrderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SnippyGrab-order-tests-" + Guid.NewGuid().ToString("N"));
    private readonly CaptureRepository repository;
    public TransferOrderTests() => repository = new(root);
    [Fact]
    public void ReverseNonadjacentSelectionUsesCurrentShelfNumbersAndExactMembership()
    {
        for (var i = 0; i < 20; i++) repository.Add([(byte)i], 1, 1);
        var items = repository.Captures;
        var paths = TransferPayload.Files(repository, [items[17], items[5], items[1], items[5]]);
        Assert.Equal(new[] { repository.PathFor(items[1]), repository.PathFor(items[5]), repository.PathFor(items[17]) }, paths);
    }
    [Fact]
    public void ReorderingAndRestartChangePayloadOrderPredictably()
    {
        var a = repository.Add([1], 1, 1); var b = repository.Add([2], 1, 1); var c = repository.Add([3], 1, 1);
        var items = (List<CaptureRecord>)repository.Captures; ShelfOrder.Move(items, a.Id, c.Id); repository.Persist();
        Assert.Equal(new[] { repository.PathFor(a), repository.PathFor(c) }, TransferPayload.Files(repository, [c, a]));
        var reopened = new CaptureRepository(root); reopened.Load();
        Assert.Equal(TransferPayload.Files(repository, [c, a]), TransferPayload.Files(reopened, [c, a]));
        Assert.DoesNotContain(repository.PathFor(b), TransferPayload.Files(repository, [c, a]));
    }
    [Fact]
    public void StaleRevisionAndUnknownIdentityAreRejectedInsteadOfChangingMembership()
    {
        var record = repository.Add([1], 1, 1); var stale = new CaptureRecord { Id = record.Id, FileName = record.FileName };
        repository.Replace(record, [2], 1, 1);
        Assert.Throws<IOException>(() => TransferPayload.Files(repository, [stale]));
        Assert.Throws<IOException>(() => TransferPayload.Files(repository, [new CaptureRecord { FileName = record.FileName }]));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
