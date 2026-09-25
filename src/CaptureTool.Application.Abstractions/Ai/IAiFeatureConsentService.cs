using CaptureTool.Domain.Ai;

namespace CaptureTool.Application.Abstractions.Ai;

public interface IAiFeatureConsentService
{
    /// <summary>Consent for capture analysis and other AI editing tools. Standalone Text Extraction uses ITextExtractionConsentService.</summary>
    AiFeatureConsentState GetConsentState(AiFeatureId featureId);
    Task<bool> EnsureConsentAsync(CancellationToken cancellationToken = default);
    CancellationToken Revoked { get; }
}

