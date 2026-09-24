using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Ai;

namespace CaptureTool.Application.Ai;

/// <summary>Editor-facing access to the same consent used by automatic capture analysis.</summary>
internal sealed class AiFeatureConsentService : IAiFeatureConsentService, IDisposable
{
    private readonly ICaptureMemoryService _memory;
    private readonly object _gate = new();
    private readonly List<CancellationTokenSource> _retired = [];
    private CancellationTokenSource _revoked = new();
    private bool _allowed;

    public AiFeatureConsentService(ICaptureMemoryService memory)
    {
        _memory = memory;
        _memory.StateChanged += OnStateChanged;
        OnStateChanged();
    }
    public CancellationToken Revoked { get { lock (_gate) return _revoked.Token; } }
    public AiFeatureConsentState GetConsentState(AiFeatureId featureId)
    {
        var state = _memory.State;
        return !state.ConsentAvailable ? AiFeatureConsentState.Unknown :
            state.Policy.ConsentGranted ? AiFeatureConsentState.Granted : AiFeatureConsentState.Denied;
    }
    public Task<bool> EnsureConsentAsync(CancellationToken cancellationToken = default) => _memory.EnsureConsentAsync(cancellationToken);
    private void OnStateChanged()
    {
        lock (_gate)
        {
            bool allowed = GetConsentState(default) == AiFeatureConsentState.Granted;
            if (allowed && !_allowed)
            {
                _retired.Add(_revoked);
                _revoked = new();
            }
            else if (!allowed) _ = ObserveCancellationAsync(_revoked);
            _allowed = allowed;
        }
    }
    private static async Task ObserveCancellationAsync(CancellationTokenSource source)
    {
        try { await source.CancelAsync().ConfigureAwait(false); } catch (Exception) { }
    }
    public void Dispose()
    {
        _memory.StateChanged -= OnStateChanged;
        lock (_gate)
        {
            _revoked.Dispose();
            foreach (var source in _retired) source.Dispose();
        }
    }
}
