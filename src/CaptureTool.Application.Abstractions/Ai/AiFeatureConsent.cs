using CaptureTool.Domain.Ai;

namespace CaptureTool.Application.Abstractions.Ai;

public sealed record AiFeatureConsent(
    AiFeatureId FeatureId,
    string DisplayName,
    AiFeatureConsentState State);

