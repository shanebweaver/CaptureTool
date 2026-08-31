namespace CaptureTool.Application.Analysis.Maintenance;

/// <summary>
/// Serializes cleanup against enrollment restoration. Never hold this gate while calling
/// the cleanup coordinator: it acquires the same gate and rechecks the durable tombstone.
/// Model preparation and source analysis must not hold it.
/// </summary>
internal sealed class CaptureAnalysisEnrollmentGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(_gate);
    }

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim? _gate = gate;
        public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
    }
}
