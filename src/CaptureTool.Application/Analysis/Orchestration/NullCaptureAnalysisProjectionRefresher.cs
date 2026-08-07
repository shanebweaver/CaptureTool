using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Domain;

namespace CaptureTool.Application.Analysis.Orchestration;

internal sealed class NullCaptureAnalysisProjectionRefresher :
    ICaptureAnalysisProjectionRefresher,
    ICaptureAnalysisProjectionMaintenance
{
    public ValueTask RefreshAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("A projection refresh requires a capture ID.", nameof(captureId));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("Projection removal requires a capture ID.", nameof(captureId));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    public ValueTask<int> RebuildAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(0);
    }
}
