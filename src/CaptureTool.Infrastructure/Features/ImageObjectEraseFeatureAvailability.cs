using CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class ImageObjectEraseFeatureAvailability : IImageObjectEraseFeatureAvailability
{
    private readonly IFeatureManager _featureManager;
    private readonly IImageObjectEraseService? _objectEraseService;

    public ImageObjectEraseFeatureAvailability(
        IFeatureManager featureManager,
        IImageObjectEraseService? objectEraseService = null)
    {
        _featureManager = featureManager;
        _objectEraseService = objectEraseService;
    }

    public bool IsImageObjectEraseEnabled =>
        _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_ObjectErase) &&
        IsSupportedOnCurrentDevice();

    private bool IsSupportedOnCurrentDevice()
    {
        if (_objectEraseService is null)
        {
            return true;
        }

        return _objectEraseService.GetReadyState() is
            ObjectEraseReadyState.Ready or
            ObjectEraseReadyState.PreparationNeeded;
    }
}
