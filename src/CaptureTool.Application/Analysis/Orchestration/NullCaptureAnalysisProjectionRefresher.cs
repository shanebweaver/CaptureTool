using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Domain;

namespace CaptureTool.Application.Analysis.Orchestration;

internal sealed class NullCaptureAnalysisProjectionRefresher : ICaptureAnalysisProjectionRefresher
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
}
