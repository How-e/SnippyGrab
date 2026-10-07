namespace SnippyGrab.Core;

// System events can already be queued when shutdown unsubscribes their handlers.
public sealed class LifecycleDispatch(Action<Action> enqueue) : IDisposable
{
    private bool disposed;
    public void Post(Action action) => enqueue(() => { if (!disposed) action(); });
    public void Dispose() => disposed = true;
}
