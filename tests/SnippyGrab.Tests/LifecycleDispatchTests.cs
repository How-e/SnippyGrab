using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class LifecycleDispatchTests
{
    [Fact]
    public void QueuedOsEventsCannotTouchDisposedWindows()
    {
        var pending = new Queue<Action>(); var calls = 0;
        var dispatch = new LifecycleDispatch(pending.Enqueue);
        dispatch.Post(() => calls++); pending.Dequeue()(); Assert.Equal(1, calls);
        dispatch.Post(() => calls++); dispatch.Dispose(); pending.Dequeue()();
        dispatch.Post(() => calls++); pending.Dequeue()(); Assert.Equal(1, calls);
    }
}
