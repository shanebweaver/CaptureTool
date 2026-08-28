using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Queries;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using System.Runtime.CompilerServices;

namespace CaptureTool.Application.Analysis.Queries;

internal sealed class CaptureAnalysisQueryService : ICaptureAnalysisQueryService
{
    private readonly ICaptureAnalysisStore _store;

    public CaptureAnalysisQueryService(ICaptureAnalysisStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async ValueTask<CaptureAnalysisRecord?> GetAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Analysis lookup requires a capture ID.", nameof(captureId));
        }

        CaptureAnalysisStoreSnapshot? snapshot = await _store
            .GetAsync(captureId, cancellationToken)
            .ConfigureAwait(false);
        return snapshot?.Record;
    }

    public async ValueTask<CapabilityAnalysis?> GetCapabilityAsync(
        CaptureId captureId,
        CapabilityDefinition capability,
        CancellationToken cancellationToken = default)
    {
        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException(
                "Analysis lookup requires a capability definition.",
                nameof(capability));
        }

        CaptureAnalysisRecord? record = await GetAsync(captureId, cancellationToken)
            .ConfigureAwait(false);
        return record != null && record.TryGetAnalysis(capability.Id, out CapabilityAnalysis? analysis) &&
            analysis?.Capability == capability
                ? analysis
                : null;
    }

    public async IAsyncEnumerable<CaptureAnalysisRecord> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (CaptureAnalysisStoreSnapshot snapshot in _store
            .ReadAllAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return snapshot.Record;
        }
    }
}
