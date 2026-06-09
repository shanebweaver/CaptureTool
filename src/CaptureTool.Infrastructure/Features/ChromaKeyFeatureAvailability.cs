using CaptureTool.Application.Abstractions.Features.ImageEdit.ChromaKey;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class ChromaKeyFeatureAvailability : IChromaKeyFeatureAvailability
{
    private readonly IFeatureManager _featureManager;

    public ChromaKeyFeatureAvailability(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    public bool IsChromaKeyEnabled => _featureManager.IsEnabled(AppFeatures.Feature_ImageEdit_ChromaKey);
}
