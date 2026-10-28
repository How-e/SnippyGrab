using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace SnippyGrab.App.Services;

internal enum ResourceSampleReason { Startup, Periodic, WindowClosed, WindowRecovery, SessionStop }
internal sealed record ResourceSnapshot(long WorkingBytes, long PrivateBytes, long PeakWorkingBytes, double CpuMs,
    int Handles, int Threads, uint GdiHandles, uint UserHandles, long ManagedBytes, long HeapBytes,
    long FragmentedBytes, int Gen0, int Gen1, int Gen2);

internal sealed class LocalSessionDiagnostics : IDisposable
{
    private sealed record Entry(string? Text, TaskCompletionSource? Barrier = null);
    private readonly object gate = new();
    private readonly string root;
    private readonly Func<Action, Task> post;
    private readonly Func<ResourceSnapshot> read;
    private readonly TimeSpan interval;
    private readonly int maximumBytes;
    private readonly Channel<Entry> queue = Channel.CreateBounded<Entry>(new BoundedChannelOptions(64) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly Lazy<Task> writer;
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly CancellationTokenSource lifetime = new();
    private Timer? timer;
    private Task sampling = Task.CompletedTask;
    private Task recovery = Task.CompletedTask;
    private bool enabled;
    private bool disposed;
    private int generation;
    private long dropped;
    private long sampledPeakPrivate;
    private int sampledPeakHandles;
    private double lastSeconds;
    private double lastCpuMs;
    private bool hasPrevious;
    internal string Session { get; } = Guid.NewGuid().ToString("N");
    internal string Build { get; }
    internal bool Enabled { get { lock (gate) return enabled; } }
    internal LocalSessionDiagnostics(string root, Func<Action, Task> post, Func<ResourceSnapshot>? read = null,
        TimeSpan? interval = null, int maximumBytes = 1024 * 1024)
    {
        this.root = root; this.post = post; this.read = read ?? ReadProcess;
        this.interval = interval ?? TimeSpan.FromMinutes(1); this.maximumBytes = maximumBytes;
        if (this.interval <= TimeSpan.Zero || maximumBytes < 2048) throw new ArgumentOutOfRangeException(nameof(interval));
        Build = typeof(LocalSessionDiagnostics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? BuildVersion.Display;
        writer = new(() => Task.Run(WriteLoopAsync));
    }
    internal void SetEnabled(bool value)
    {
        lock (gate)
        {
            if (disposed || enabled == value) return;
            enabled = value; generation++; hasPrevious = false;
            timer?.Dispose(); timer = value ? new Timer(_ => _ = SampleAsync(ResourceSampleReason.Periodic), null, interval, interval) : null;
        }
        if (value) _ = SampleAsync(ResourceSampleReason.Startup);
    }
    internal Task SampleAsync(ResourceSampleReason reason = ResourceSampleReason.Periodic)
    {
        lock (gate)
        {
            if (!enabled || disposed || !sampling.IsCompleted) return sampling;
            var version = generation;
            return sampling = Task.Run(async () =>
            {
                try
                {
                    var snapshot = read(); var seconds = elapsed.Elapsed.TotalSeconds;
                    var atUtc = DateTimeOffset.UtcNow; var queuedAt = Stopwatch.GetTimestamp();
                    await post(() =>
                    {
                        lock (gate)
                        {
                            if (!enabled || disposed || version != generation) return;
                            var cpuPercent = hasPrevious && seconds > lastSeconds
                                ? Math.Max(0, snapshot.CpuMs - lastCpuMs) / ((seconds - lastSeconds) * 10) : (double?)null;
                            lastSeconds = seconds; lastCpuMs = snapshot.CpuMs; hasPrevious = true;
                            sampledPeakPrivate = Math.Max(sampledPeakPrivate, snapshot.PrivateBytes);
                            sampledPeakHandles = Math.Max(sampledPeakHandles, snapshot.Handles);
                            var text = JsonSerializer.Serialize(new
                            {
                                AtUtc = atUtc,
                                Session,
                                Build,
                                Reason = reason.ToString(),
                                ElapsedSeconds = seconds,
                                Resources = snapshot,
                                CpuPercentOfOneCore = cpuPercent,
                                SampledPeakPrivateBytes = sampledPeakPrivate,
                                SampledPeakHandles = sampledPeakHandles,
                                DispatcherLagMs = Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds,
                                DroppedSamples = Interlocked.Read(ref dropped)
                            });
                            _ = writer.Value;
                            if (!queue.Writer.TryWrite(new(text))) Interlocked.Increment(ref dropped);
                        }
                    });
                }
                catch (Exception) { Interlocked.Increment(ref dropped); /* Optional diagnostics must not interrupt capture or fail a timer callback. */ }
            });
        }
    }
    internal void WindowClosed()
    {
        _ = SampleAsync(ResourceSampleReason.WindowClosed);
        lock (gate)
        {
            if (!enabled || disposed || !recovery.IsCompleted) return;
            var version = generation;
            recovery = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), lifetime.Token);
                    lock (gate) { if (!enabled || disposed || version != generation) return; }
                    await SampleAsync(ResourceSampleReason.WindowRecovery);
                }
                catch (OperationCanceledException) { }
            });
        }
    }
    internal async Task DrainAsync()
    {
        if (!writer.IsValueCreated) return;
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try { await queue.Writer.WriteAsync(new(null, barrier)); await barrier.Task; }
        catch (ChannelClosedException) { await writer.Value; }
    }
    private async Task WriteLoopAsync()
    {
        await foreach (var entry in queue.Reader.ReadAllAsync())
        {
            if (entry.Barrier is not null) { entry.Barrier.TrySetResult(); continue; }
            try
            {
                var path = Path.Combine(root, "resources.log"); var previous = Path.Combine(root, "resources.previous.log");
                ManagedPath.RejectRedirects(path); ManagedPath.RejectRedirects(previous); Directory.CreateDirectory(root);
                var text = entry.Text + "\n"; var bytes = Encoding.UTF8.GetByteCount(text);
                if (bytes > maximumBytes) { Interlocked.Increment(ref dropped); continue; }
                if (File.Exists(path) && new FileInfo(path).Length + bytes > maximumBytes) File.Move(path, previous, true);
                File.AppendAllText(path, text);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            { Interlocked.Increment(ref dropped); }
        }
    }
    internal async Task StopAsync()
    {
        await SampleAsync(ResourceSampleReason.SessionStop);
        Task active;
        lock (gate) { active = sampling; }
        Dispose(); await active;
        if (writer.IsValueCreated) await writer.Value;
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true; enabled = false; generation++; timer?.Dispose(); timer = null;
            lifetime.Cancel(); queue.Writer.TryComplete();
        }
    }
    private static ResourceSnapshot ReadProcess()
    {
        using var process = Process.GetCurrentProcess(); process.Refresh(); var gc = GC.GetGCMemoryInfo();
        return new(process.WorkingSet64, process.PrivateMemorySize64, process.PeakWorkingSet64, process.TotalProcessorTime.TotalMilliseconds,
            process.HandleCount, process.Threads.Count, GetGuiResources(process.Handle, 0), GetGuiResources(process.Handle, 1),
            GC.GetTotalMemory(false), gc.HeapSizeBytes, gc.FragmentedBytes, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
    }
    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(nint process, uint flags);
}
