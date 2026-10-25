using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis;

/// <summary>Device support, independent of consent, rollout flags, and model download readiness.</summary>
public interface IAnalysisFeatureAvailability
{
    Task<bool> IsSupportedAsync(AnalysisMediaKind kind, AnalysisCapability capability,
        CancellationToken cancellationToken = default);
    void Invalidate();
}
