using System.Diagnostics;
using System.Text.Json.Serialization;

namespace SnippyGrab.Core;

public enum CapturePhase { Hide, Acquisition, Crop, EncodingQueue, Encoding, Storage, Clipboard }
public enum CaptureOutcome { Cancelled, Completed, ClipboardUnavailable, ClipboardOnly, PreservationFailed, Failed }
public sealed record CaptureTimingResult(string Operation,
    [property: JsonConverter(typeof(JsonStringEnumConverter<CaptureMode>))] CaptureMode Mode,
    [property: JsonConverter(typeof(JsonStringEnumConverter<CaptureOutcome>))] CaptureOutcome Status, double InteractiveTotalMs,
    double? RequestToOverlayMs, double? SelectionToStoredMs, double? SelectionToClipboardMs, bool? ClipboardCopied,
    bool? HistorySaved, string? ExceptionType, IReadOnlyDictionary<string, double> PhaseMilliseconds);

public sealed class CaptureTiming(CaptureMode mode, Func<double>? clock = null)
{
    private readonly object gate = new();
    private readonly Func<double> now = clock ?? (() => Stopwatch.GetTimestamp() * 1000d / Stopwatch.Frequency);
    private readonly string operation = Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, double> phases = [];
    private double? started;
    private double? selected;
    private double? overlay;
    private double? stored;
    private double? copied;
    private bool? clipboardCopied;
    private bool? historySaved;
    private string? exceptionType;
    private CaptureOutcome outcome = CaptureOutcome.Cancelled;
    private CaptureTimingResult? result;
    public string Operation => operation;
    public void Start() { lock (gate) started ??= now(); }
    public void OverlayShown() { lock (gate) { Start(); overlay ??= Math.Max(0, now() - started!.Value); } }
    public void SelectionCompleted() { lock (gate) selected ??= now(); }
    public IDisposable Measure(CapturePhase phase) => new Phase(this, phase, now());
    public void Stored(bool saved) { lock (gate) { historySaved = saved; if (selected is { } at) stored = Math.Max(0, now() - at); } }
    public void ClipboardPublished(bool success)
    {
        lock (gate) { clipboardCopied = success; if (success && selected is { } at) copied = Math.Max(0, now() - at); }
    }
    public void Complete(CaptureOutcome value) { lock (gate) outcome = value; }
    public void Fail(Exception error)
    {
        lock (gate) { outcome = error is OperationCanceledException ? CaptureOutcome.Cancelled : CaptureOutcome.Failed; exceptionType = error.GetType().Name; }
    }
    public CaptureTimingResult Finish()
    {
        lock (gate)
        {
            Start();
            return result ??= new(operation, mode, outcome, Math.Max(0, now() - started!.Value), overlay, stored, copied,
                clipboardCopied, historySaved, exceptionType, new Dictionary<string, double>(phases));
        }
    }
    private sealed class Phase(CaptureTiming owner, CapturePhase phase, double started) : IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lock (owner.gate)
            {
                var key = phase.ToString();
                owner.phases[key] = owner.phases.GetValueOrDefault(key) + Math.Max(0, owner.now() - started);
            }
        }
    }
}
