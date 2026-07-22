using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class TextExtractionFeatureAvailability : ITextExtractionFeatureAvailability
{
    private readonly IFeatureManager _featureManager;
    private readonly ITextExtractionService? _textExtractionService;

    public TextExtractionFeatureAvailability(
        IFeatureManager featureManager,
        ITextExtractionService? textExtractionService = null)
    {
        _featureManager = featureManager;
        _textExtractionService = textExtractionService;
    }

    public bool IsTextExtractionEnabled =>
        _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_TextExtraction) &&
        IsSupportedOnCurrentDevice();

    private bool IsSupportedOnCurrentDevice()
    {
        if (_textExtractionService is null)
        {
            return true;
        }

        return _textExtractionService.GetReadyState() is
            TextExtractionReadyState.Ready or
            TextExtractionReadyState.PreparationNeeded;
    }
}

