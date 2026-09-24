using CaptureTool.Domain.Ai;

namespace CaptureTool.Application.Abstractions.Ai;

public interface IAiFeatureConsentService
{
    /// <summary>Every feature shares the same protected, application-wide consent.</summary>
    AiFeatureConsentState GetConsentState(AiFeatureId featureId);
    Task<bool> EnsureConsentAsync(CancellationToken cancellationToken = default);
    CancellationToken Revoked { get; }
}

