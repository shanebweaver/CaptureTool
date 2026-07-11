using CaptureTool.Domain.Ai;

namespace CaptureTool.Application.Abstractions.Ai;

public interface IAiFeatureConsentDialogService
{
    Task<bool> RequestConsentAsync(AiFeatureId featureId, CancellationToken cancellationToken = default);
}

