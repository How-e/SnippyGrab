namespace SnippyGrab.Core;

// Used on the UI thread: close and Exit join an in-flight apply instead of starting another one.
public sealed class EditorCommitCoordinator
{
    private Task<bool>? pending;
    public bool Busy => pending is { IsCompleted: false };

    public Task<bool> RunAsync(Func<Task<bool>> apply)
    {
        if (Busy) return pending!;
        pending = ExecuteAsync(apply);
        return pending;
    }

    private static async Task<bool> ExecuteAsync(Func<Task<bool>> apply)
    {
        // Install the shared task before invoking code that may synchronously request close.
        await Task.Yield();
        return await apply();
    }
}
