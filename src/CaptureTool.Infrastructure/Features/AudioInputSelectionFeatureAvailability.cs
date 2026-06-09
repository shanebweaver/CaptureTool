using CaptureTool.Application.Abstractions.Features.CaptureOverlay;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class AudioInputSelectionFeatureAvailability : IAudioInputSelectionFeatureAvailability
{
    private readonly IFeatureManager _featureManager;

    public AudioInputSelectionFeatureAvailability(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    public bool IsAudioInputSelectionEnabled => _featureManager.IsEnabled(AppFeatures.Feature_AudioInputSelection);
}
