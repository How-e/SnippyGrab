namespace SnippyGrab.Core;

// A detached view follows committed revisions while retaining exactly one current source lease.
public sealed class CaptureViewLease : IDisposable
{
    private readonly CaptureRepository repository;
    private readonly CaptureRecord record;
    private IDisposable? lease;
    public CaptureViewLease(CaptureRepository repository, CaptureRecord record)
    {
        this.repository = repository; this.record = record;
        lease = repository.Lease([record]); repository.RevisionChanged += Changed;
    }
    private void Changed(CaptureRecord changed)
    {
        if (changed.Id != record.Id) return;
        var next = repository.Lease([record]); var previous = lease; lease = next; previous?.Dispose();
    }
    public void Dispose() { repository.RevisionChanged -= Changed; lease?.Dispose(); lease = null; }
}
