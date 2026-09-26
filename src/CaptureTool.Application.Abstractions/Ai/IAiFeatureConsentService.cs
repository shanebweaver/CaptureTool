using CaptureTool.Domain.Ai;

namespace CaptureTool.Application.Abstractions.Ai;

public interface IAiFeatureConsentService
{
    /// <summary>Shared consent for every local AI feature, including standalone Text Extraction.</summary>
    AiFeatureConsentState GetConsentState(AiFeatureId featureId);
    Task<bool> EnsureConsentAsync(CancellationToken cancellationToken = default);
    CancellationToken Revoked { get; }
}

