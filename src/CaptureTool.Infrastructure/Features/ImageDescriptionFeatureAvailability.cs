using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class ImageDescriptionFeatureAvailability : IImageDescriptionFeatureAvailability
{
    private readonly IFeatureManager _featureManager;
    private readonly IImageDescriptionService? _imageDescriptionService;

    public ImageDescriptionFeatureAvailability(
        IFeatureManager featureManager,
        IImageDescriptionService? imageDescriptionService = null)
    {
        _featureManager = featureManager;
        _imageDescriptionService = imageDescriptionService;
    }

    public bool IsImageDescriptionEnabled =>
        _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_ImageDescription) &&
        IsSupportedOnCurrentDevice();

    private bool IsSupportedOnCurrentDevice()
    {
        if (_imageDescriptionService is null)
        {
            return true;
        }

        return _imageDescriptionService.GetReadyState() is
            ImageDescriptionReadyState.Ready or
            ImageDescriptionReadyState.PreparationNeeded;
    }
}
