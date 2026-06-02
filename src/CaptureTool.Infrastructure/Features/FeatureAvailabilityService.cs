using CaptureTool.Application.Abstractions.Features;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class FeatureAvailabilityService : IFeatureAvailabilityService
{
    private readonly IFeatureManager _featureManager;

    public FeatureAvailabilityService(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    public bool IsAddOnsStoreEnabled => _featureManager.IsEnabled(AppFeatures.Feature_AddOns_Store);

    public bool IsAudioCaptureEnabled => _featureManager.IsEnabled(AppFeatures.Feature_AudioCapture);

    public bool IsAudioInputSelectionEnabled => _featureManager.IsEnabled(AppFeatures.Feature_AudioInputSelection);

    public bool IsImageEditChromaKeyEnabled => _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_ChromaKey);
}
