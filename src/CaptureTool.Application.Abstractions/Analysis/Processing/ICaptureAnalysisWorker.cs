namespace CaptureTool.Application.Abstractions.Analysis.Processing;

public interface ICaptureAnalysisWorker
{
    Task RunAsync(CancellationToken cancellationToken);
}
