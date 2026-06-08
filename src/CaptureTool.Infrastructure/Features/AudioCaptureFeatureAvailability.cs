using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class AudioCaptureFeatureAvailability : IAudioCaptureFeatureAvailability
{
    private readonly IFeatureManager _featureManager;

    public AudioCaptureFeatureAvailability(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    public bool IsAudioCaptureEnabled => _featureManager.IsEnabled(AppFeatures.Feature_AudioCapture);
}
