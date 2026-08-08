using CaptureTool.Domain;

namespace CaptureTool.Application.Analysis.Maintenance;

internal interface ICaptureAnalysisCleanupCoordinator
{
    ValueTask<bool> ReconcileAsync(CancellationToken cancellationToken = default);

    ValueTask<bool> ReconcileCaptureAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default);
}
