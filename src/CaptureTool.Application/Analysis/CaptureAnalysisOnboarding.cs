using CaptureTool.Application.Abstractions.Analysis;

namespace CaptureTool.Application.Analysis;

internal sealed class CaptureAnalysisOnboarding(ICaptureMemoryService memory) : ICaptureAnalysisOnboarding, IDisposable
{
    private readonly SemaphoreSlim _requests = new(1, 1);
    private long _attempt;
    public async Task<bool> EnableAsync(CancellationToken ct = default)
    {
        long attempt = Interlocked.Read(ref _attempt);
        await _requests.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await memory.InitializeAsync(ct).ConfigureAwait(false);
            if (!memory.State.PolicyAvailable) return false;
            if (memory.State.Policy.IsAllowed) return true;
            if (attempt != Interlocked.Read(ref _attempt)) return memory.State.Policy.IsAllowed;
            if (!memory.State.Policy.ConsentGranted) await memory.SetConsentAsync(true, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _attempt);
            return memory.State.Policy.IsAllowed;
        }
        finally { _requests.Release(); }
    }
    public void Dispose() => _requests.Dispose();
}
