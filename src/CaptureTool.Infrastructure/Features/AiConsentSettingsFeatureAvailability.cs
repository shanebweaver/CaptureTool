using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class AiConsentSettingsFeatureAvailability : IAiConsentSettingsFeatureAvailability
{
    private readonly IFeatureManager _featureManager;

    public AiConsentSettingsFeatureAvailability(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    public bool IsAiConsentSettingsEnabled => _featureManager.IsEnabled(AppFeatures.Feature_Settings_AiConsent);
}
