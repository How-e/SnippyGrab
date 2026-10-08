namespace SnippyGrab.Core;

// Used on the UI thread: close and Exit join an in-flight apply instead of starting another one.
public sealed class EditorCommitCoordinator
{
    private Task<bool>? pending;
    public bool Busy => pending is { IsCompleted: false };

    public Task<bool> RunAsync(Func<Task<bool>> apply)
    {
        if (Busy) return pending!;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending = completion.Task;
        _ = ExecuteAsync(apply, completion);
        return completion.Task;
    }

    private static async Task ExecuteAsync(Func<Task<bool>> apply, TaskCompletionSource<bool> completion)
    {
        // RunAsync installs the shared completion before calling code that can reenter.
        // Yielding is insufficient: some contexts dispatch before the assignment returns.
        try { completion.TrySetResult(await apply()); }
        catch (OperationCanceledException ex) { completion.TrySetCanceled(ex.CancellationToken); }
        catch (Exception ex) { completion.TrySetException(ex); }
    }
}
