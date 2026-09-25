namespace CaptureTool.Application.Abstractions.Analysis;

/// <summary>Explicit entry to capture-analysis consent and scanning; never used by standalone OCR.</summary>
public interface ICaptureAnalysisOnboarding
{
    Task ShowOnFirstLaunchAsync(CancellationToken cancellationToken = default);
    Task<bool> EnableAsync(CancellationToken cancellationToken = default);
}
