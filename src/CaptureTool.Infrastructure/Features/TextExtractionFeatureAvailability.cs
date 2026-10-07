using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

namespace CaptureTool.Infrastructure.Features;

public sealed class TextExtractionFeatureAvailability : ITextExtractionFeatureAvailability
{
    private readonly ITextExtractionService? _textExtractionService;

    public TextExtractionFeatureAvailability(
        ITextExtractionService? textExtractionService = null)
    {
        _textExtractionService = textExtractionService;
    }

    public bool IsTextExtractionEnabled => IsSupportedOnCurrentDevice();

    private bool IsSupportedOnCurrentDevice()
    {
        if (_textExtractionService is null)
        {
            return false;
        }

        return _textExtractionService.GetReadyState() is
            TextExtractionReadyState.Ready or
            TextExtractionReadyState.PreparationNeeded or
            TextExtractionReadyState.LanguageResourcesRequired;
    }
}

