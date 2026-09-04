namespace CaptureTool.Application.Analysis.Intake;

internal interface ICaptureAnalysisReconciler
{
    Task ReconcileStartupAsync(CancellationToken cancellationToken = default);

    Task ConsumePendingChangesAsync(CancellationToken cancellationToken = default);
}
