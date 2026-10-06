using System.Runtime.InteropServices;
namespace SnippyGrab.Core;

public sealed class ClipboardWriter
{
    private int generation;
    public void Invalidate() => generation++;
    public async Task<bool> WriteAsync(Action write, Func<int, Task>? delay = null, CancellationToken cancellation = default)
    {
        var operation = ++generation;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (operation != generation || cancellation.IsCancellationRequested) return false;
            try { write(); return true; }
            catch (ExternalException) { await (delay?.Invoke(25 * (attempt + 1)) ?? Task.Delay(25 * (attempt + 1))); }
        }
        return false;
    }
}

public sealed class LatestOperation : IDisposable
{
    private CancellationTokenSource? current;
    public CancellationToken Begin(CancellationToken lifetime = default)
    {
        Cancel(); current = CancellationTokenSource.CreateLinkedTokenSource(lifetime); return current.Token;
    }
    public void Cancel() { current?.Cancel(); current?.Dispose(); current = null; }
    public void Dispose() => Cancel();
}
