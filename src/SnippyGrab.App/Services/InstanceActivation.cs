namespace SnippyGrab.App.Services;

internal sealed class InstanceActivation : IDisposable
{
    private readonly EventWaitHandle signal;
    private RegisteredWaitHandle? registration;
    internal InstanceActivation(string name) => signal = new(false, EventResetMode.AutoReset, name);
    internal void Signal() => signal.Set();
    internal void Listen(Action activate) => registration = ThreadPool.RegisterWaitForSingleObject(signal, (_, timedOut) => { if (!timedOut) activate(); }, null, Timeout.Infinite, false);
    public void Dispose() { registration?.Unregister(null); signal.Dispose(); }
}
