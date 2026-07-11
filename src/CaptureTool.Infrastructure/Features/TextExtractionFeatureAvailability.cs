using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class TextExtractionFeatureAvailability : ITextExtractionFeatureAvailability
{
    private readonly IFeatureManager _featureManager;

    public TextExtractionFeatureAvailability(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    public bool IsTextExtractionEnabled => _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_TextExtraction);
}

