using CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class VideoSuperResolutionFeatureAvailability : IVideoSuperResolutionFeatureAvailability
{
    private readonly IFeatureManager _featureManager;
    private readonly IVideoSuperResolutionService? _videoSuperResolutionService;

    public VideoSuperResolutionFeatureAvailability(
        IFeatureManager featureManager,
        IVideoSuperResolutionService? videoSuperResolutionService = null)
    {
        _featureManager = featureManager;
        _videoSuperResolutionService = videoSuperResolutionService;
    }

    public bool IsVideoSuperResolutionEnabled =>
        _featureManager.IsEnabled(AppFeatures.Feature_VideoEdit_SuperResolution) &&
        IsSupportedOnCurrentDevice();

    private bool IsSupportedOnCurrentDevice()
    {
        if (_videoSuperResolutionService is null)
        {
            return true;
        }

        return _videoSuperResolutionService.GetReadyState() is
            VideoSuperResolutionReadyState.Ready or
            VideoSuperResolutionReadyState.PreparationNeeded;
    }
}
