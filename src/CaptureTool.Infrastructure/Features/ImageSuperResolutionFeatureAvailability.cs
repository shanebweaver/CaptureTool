using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class ImageSuperResolutionFeatureAvailability : IImageSuperResolutionFeatureAvailability
{
    private readonly IFeatureManager _featureManager;
    private readonly IImageSuperResolutionService? _imageSuperResolutionService;

    public ImageSuperResolutionFeatureAvailability(
        IFeatureManager featureManager,
        IImageSuperResolutionService? imageSuperResolutionService = null)
    {
        _featureManager = featureManager;
        _imageSuperResolutionService = imageSuperResolutionService;
    }

    public bool IsImageSuperResolutionEnabled =>
        _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_SuperResolution) &&
        IsSupportedOnCurrentDevice();

    private bool IsSupportedOnCurrentDevice()
    {
        if (_imageSuperResolutionService is null)
        {
            return true;
        }

        return _imageSuperResolutionService.GetReadyState() is
            ImageSuperResolutionReadyState.Ready or
            ImageSuperResolutionReadyState.PreparationNeeded;
    }
}
