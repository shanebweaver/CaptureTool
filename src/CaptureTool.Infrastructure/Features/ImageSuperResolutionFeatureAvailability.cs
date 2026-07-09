using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class ImageSuperResolutionFeatureAvailability : IImageSuperResolutionFeatureAvailability
{
    private readonly IFeatureManager _featureManager;

    public ImageSuperResolutionFeatureAvailability(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    public bool IsImageSuperResolutionEnabled => _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_SuperResolution);
}
