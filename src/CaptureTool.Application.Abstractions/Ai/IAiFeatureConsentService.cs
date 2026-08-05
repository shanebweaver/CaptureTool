using CaptureTool.Domain.Ai;

namespace CaptureTool.Application.Abstractions.Ai;

public interface IAiFeatureConsentService
{
    IReadOnlyList<AiFeatureConsent> GetFeatureConsents();

    AiFeatureConsentState GetConsentState(AiFeatureId featureId);

    Task<bool> SetConsentAsync(AiFeatureId featureId, bool isGranted, CancellationToken cancellationToken = default);
}

