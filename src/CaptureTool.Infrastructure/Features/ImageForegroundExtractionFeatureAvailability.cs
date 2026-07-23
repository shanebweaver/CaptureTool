using CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class ImageForegroundExtractionFeatureAvailability : IImageForegroundExtractionFeatureAvailability
{
    private readonly IFeatureManager _featureManager;
    private readonly IImageForegroundExtractionService? _foregroundExtractionService;

    public ImageForegroundExtractionFeatureAvailability(
        IFeatureManager featureManager,
        IImageForegroundExtractionService? foregroundExtractionService = null)
    {
        _featureManager = featureManager;
        _foregroundExtractionService = foregroundExtractionService;
    }

    public bool IsImageForegroundExtractionEnabled =>
        _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_ForegroundExtraction) &&
        IsSupportedOnCurrentDevice();

    private bool IsSupportedOnCurrentDevice()
    {
        if (_foregroundExtractionService is null)
        {
            return true;
        }

        return _foregroundExtractionService.GetReadyState() is
            ForegroundExtractionReadyState.Ready or
            ForegroundExtractionReadyState.PreparationNeeded;
    }
}
