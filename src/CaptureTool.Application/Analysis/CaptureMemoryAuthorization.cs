using CaptureTool.Application.Abstractions.Analysis;

namespace CaptureTool.Application.Analysis;

internal sealed class CaptureMemoryAuthorization(ICaptureMemoryPolicyStore store) : IAnalysisAuthorization, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _blockGate = new();
    private readonly List<CancellationTokenSource> _retired = [];
    private CancellationTokenSource _revoked = new();
    private CaptureMemoryPolicy _policy = CaptureMemoryPolicy.Disabled();
    private bool _blocked = true;
    private long _blockVersion;
    public CaptureMemoryPolicy Policy => Volatile.Read(ref _policy);
    public bool IsAllowed => !Volatile.Read(ref _blocked) && Policy.IsAllowed;
    public bool IsLoaded { get; private set; }

    public async Task InitializeAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsLoaded) return;
            long blockVersion = Interlocked.Read(ref _blockVersion);
            CaptureMemoryPolicy? stored = await store.LoadAsync(ct).ConfigureAwait(false);
            CaptureMemoryPolicy policy = stored ?? CaptureMemoryPolicy.Disabled();
            if (stored == null) await store.SaveAsync(policy, ct).ConfigureAwait(false);
            Volatile.Write(ref _policy, policy);
            lock (_blockGate)
                if (blockVersion == _blockVersion) Volatile.Write(ref _blocked, false);
            IsLoaded = true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) { throw new CaptureMemoryPolicyException("policy-unavailable", exception); }
        finally { _gate.Release(); }
    }

    public void Block()
    {
        lock (_blockGate)
        {
            Interlocked.Increment(ref _blockVersion);
            Volatile.Write(ref _blocked, true);
        }
        _ = ObserveCancellationAsync(Volatile.Read(ref _revoked));
    }

    public async Task SaveAsync(CaptureMemoryPolicy policy, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!IsLoaded) throw new InvalidOperationException("Capture memory policy is unavailable.");
            if (!policy.IsAllowed) Block();
            long blockVersion = Interlocked.Read(ref _blockVersion);
            try { await store.SaveAsync(policy, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { Block(); throw; }
            catch (Exception exception) { Block(); throw new CaptureMemoryPolicyException("policy-save", exception); }
            if (policy.Revision != Policy.Revision || _revoked.IsCancellationRequested)
            {
                // Old leases keep a valid revoked token until application teardown.
                _retired.Add(_revoked);
                _ = ObserveCancellationAsync(_revoked);
                Volatile.Write(ref _revoked, new CancellationTokenSource());
            }
            Volatile.Write(ref _policy, policy);
            // A newer disable/revocation may arrive while the protected write is in flight.
            // Persisting this older command must never undo that immediate fence.
            lock (_blockGate)
                if (blockVersion == _blockVersion) Volatile.Write(ref _blocked, false);
        }
        finally { _gate.Release(); }
    }

    public async ValueTask<IAnalysisAuthorizationLease> AcquireAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(IsLoaded && IsAllowed, Policy.Revision, _revoked.Token, _gate);
    }
    private static async Task ObserveCancellationAsync(CancellationTokenSource source)
    {
        try { await source.CancelAsync().ConfigureAwait(false); } catch (Exception) { }
    }
    public void Dispose()
    {
        _revoked.Dispose();
        foreach (var source in _retired) source.Dispose();
        _gate.Dispose();
    }
    private sealed class Lease(bool allowed, Guid revision, CancellationToken revoked, SemaphoreSlim gate) : IAnalysisAuthorizationLease
    {
        public bool IsAllowed => allowed;
        public Guid Revision => revision;
        public CancellationToken Revoked => revoked;
        public void Dispose() => gate.Release();
    }
}

internal sealed class CaptureMemoryPolicyException(string code, Exception inner) : Exception("Capture memory policy operation failed.", inner)
{
    public string Code { get; } = code;
}
