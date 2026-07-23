using CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectExtraction;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class ImageObjectExtractionFeatureAvailability : IImageObjectExtractionFeatureAvailability
{
    private readonly IFeatureManager _featureManager;
    private readonly IImageForegroundExtractionService? _objectExtractionService;

    public ImageObjectExtractionFeatureAvailability(
        IFeatureManager featureManager,
        IImageForegroundExtractionService? objectExtractionService = null)
    {
        _featureManager = featureManager;
        _objectExtractionService = objectExtractionService;
    }

    public bool IsImageObjectExtractionEnabled =>
        _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_ObjectExtraction) &&
        IsSupportedOnCurrentDevice();

    private bool IsSupportedOnCurrentDevice()
    {
        if (_objectExtractionService is null)
        {
            return true;
        }

        return _objectExtractionService.GetReadyState() is
            ForegroundExtractionReadyState.Ready or
            ForegroundExtractionReadyState.PreparationNeeded;
    }
}
