namespace CaptureTool.Application.Abstractions.Analysis;

/// <summary>Consent requested only by an explicit AI action; never used by standalone OCR.</summary>
public interface ICaptureAnalysisOnboarding
{
    Task<bool> EnableAsync(CancellationToken cancellationToken = default);
}
