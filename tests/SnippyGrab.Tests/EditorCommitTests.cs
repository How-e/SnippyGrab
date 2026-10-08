using SnippyGrab.Core;

namespace SnippyGrab.Tests;

public sealed class EditorCommitTests
{
    private sealed class InlineSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) => callback(state);
    }

    [Fact]
    public async Task ReentrantCloseJoinsWhenDispatchRunsBeforeTaskAssignment()
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new InlineSynchronizationContext());
        try
        {
            var gate = new EditorCommitCoordinator();
            Task<bool>? close = null;
            var calls = 0;
            var apply = gate.RunAsync(() =>
            {
                calls++;
                close = gate.RunAsync(() => throw new InvalidOperationException("Duplicate apply"));
                return Task.FromResult(true);
            });
            Assert.Same(apply, close);
            Assert.True(await apply);
            Assert.Equal(1, calls);
            Assert.False(gate.Busy);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    [Fact]
    public async Task ReentrantCloseJoinsApplyBeforeItsFirstAwait()
    {
        var gate = new EditorCommitCoordinator();
        Task<bool>? close = null;
        var calls = 0;
        var apply = gate.RunAsync(() =>
        {
            calls++;
            close = gate.RunAsync(() => throw new InvalidOperationException("Duplicate apply"));
            return Task.FromResult(true);
        });
        Assert.True(await apply);
        Assert.Same(apply, close);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CancelledApplyDoesNotPoisonRetryOrAnotherEditor()
    {
        var first = new EditorCommitCoordinator();
        var second = new EditorCommitCoordinator();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.RunAsync(() => Task.FromCanceled<bool>(cancellation.Token)));
        Assert.False(first.Busy);
        Assert.True(await second.RunAsync(() => Task.FromResult(true)));
        Assert.True(await first.RunAsync(() => Task.FromResult(true)));
    }

    [Fact]
    public async Task CloseAndExitWaitForTheSamePendingClipboardWork()
    {
        var gate = new EditorCommitCoordinator();
        var clipboard = new TaskCompletionSource<bool>();
        var calls = 0;
        Task<bool> Apply() { calls++; return clipboard.Task; }
        var copy = gate.RunAsync(Apply);
        var close = gate.RunAsync(Apply);
        var exit = gate.RunAsync(Apply);
        Assert.Same(copy, close); Assert.Same(copy, exit);
        Assert.True(gate.Busy); Assert.False(exit.IsCompleted);
        clipboard.SetResult(true);
        Assert.True(await exit); Assert.Equal(1, calls); Assert.False(gate.Busy);
    }

    [Fact]
    public async Task ExhaustedClipboardRetriesAllowAnotherAttempt()
    {
        var gate = new EditorCommitCoordinator();
        Assert.False(await gate.RunAsync(() => Task.FromResult(false)));
        Assert.False(gate.Busy);
        Assert.True(await gate.RunAsync(() => Task.FromResult(true)));
    }

    [Fact]
    public async Task StorageFailureReachesCloseAndAllowsRetry()
    {
        var gate = new EditorCommitCoordinator();
        var storage = new TaskCompletionSource<bool>();
        var copy = gate.RunAsync(() => storage.Task);
        var close = gate.RunAsync(() => Task.FromResult(true));
        Assert.Same(copy, close);
        storage.SetException(new IOException("Storage unavailable"));
        await Assert.ThrowsAsync<IOException>(() => close);
        Assert.True(await gate.RunAsync(() => Task.FromResult(true)));
    }

    [Fact]
    public async Task MultipleEditorsKeepIndependentPendingWork()
    {
        var first = new EditorCommitCoordinator(); var second = new EditorCommitCoordinator();
        var clipboard = new TaskCompletionSource<bool>();
        var pending = first.RunAsync(() => clipboard.Task);
        Assert.True(await second.RunAsync(() => Task.FromResult(true)));
        Assert.True(first.Busy); Assert.False(pending.IsCompleted);
        clipboard.SetResult(false); Assert.False(await pending);
    }
}
