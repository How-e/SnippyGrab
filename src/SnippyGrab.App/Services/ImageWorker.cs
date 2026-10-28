using System.Windows.Threading;

namespace SnippyGrab.App.Services;

// WPF imaging creates thread-local dispatchers/media resources. Reuse bounded STAs
// instead of leaving such resources on a growing set of thread-pool threads.
// Dispatcher cancellation removes superseded work before decode/allocation.
internal static class ImageWorker
{
    private static readonly Lazy<Dispatcher> Worker = new(() => Start("SnippyGrab image worker"));
    private static readonly Lazy<Dispatcher> CaptureWorker = new(() => Start("SnippyGrab capture worker"));
    private static readonly Lazy<Dispatcher> DeliveryWorker = new(() => Start("SnippyGrab delivery worker"));
    // Native recognition cannot be preempted mid-pass. Keep it off the dispatcher
    // used by newer copies, previews, editor work and capture PNG delivery.
    private static readonly Lazy<Dispatcher> OcrWorker = new(() => Start("SnippyGrab OCR worker"));
    private static Dispatcher Start(string name)
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { ready.SetResult(Dispatcher.CurrentDispatcher); Dispatcher.Run(); }) { IsBackground = true, Name = name };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    }
    internal static Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellation = default)
        => RunAsync(Worker, work, cancellation);
    internal static Task<T> RunCaptureAsync<T>(Func<T> work, CancellationToken cancellation = default)
        => RunAsync(CaptureWorker, work, cancellation);
    internal static Task<T> RunDeliveryAsync<T>(Func<T> work, CancellationToken cancellation = default)
        => RunAsync(DeliveryWorker, work, cancellation);
    internal static Task<T> RunOcrAsync<T>(Func<T> work, CancellationToken cancellation = default)
        => RunAsync(OcrWorker, work, cancellation);
    private static Task<T> RunAsync<T>(Lazy<Dispatcher> worker, Func<T> work, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var dispatcher = worker.Value;
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
