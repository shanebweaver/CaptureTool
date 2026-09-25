namespace CaptureTool.Application.Abstractions.Analysis;

/// <summary>Releases reusable provider resources once inference has stopped and the queue is idle.</summary>
public interface IAnalysisResources
{
    Task ReleaseAsync();
}
