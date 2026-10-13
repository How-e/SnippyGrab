using System.Windows.Threading;

namespace SnippyGrab.App.Services;

// WPF imaging creates thread-local dispatchers/media resources. Reuse one STA
// instead of leaving such resources on a growing set of thread-pool threads.
// Dispatcher cancellation removes superseded work before decode/allocation.
internal static class ImageWorker
{
    private static readonly Lazy<Dispatcher> Worker = new(Start);
    private static Dispatcher Start()
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { ready.SetResult(Dispatcher.CurrentDispatcher); Dispatcher.Run(); }) { IsBackground = true, Name = "SnippyGrab image worker" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    }
    internal static Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var dispatcher = Worker.Value;
        if (dispatcher.CheckAccess()) return Task.FromResult(work());
        return dispatcher.InvokeAsync(() => { cancellation.ThrowIfCancellationRequested(); return work(); }, DispatcherPriority.Background, cancellation).Task;
    }
    internal static T Run<T>(Func<T> work, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var dispatcher = Worker.Value;
        return dispatcher.CheckAccess() ? work() : dispatcher.Invoke(() => { cancellation.ThrowIfCancellationRequested(); return work(); });
    }
}
