using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class CaptureMemoryFeatureAvailability : ICaptureMemoryFeatureAvailability
{
    private readonly IFeatureManager _featureManager;
    private readonly ICaptureAnalysisFeatureAvailability _analysisAvailability;

    public CaptureMemoryFeatureAvailability(
        IFeatureManager featureManager,
        ICaptureAnalysisFeatureAvailability analysisAvailability)
    {
        _featureManager = featureManager;
        _analysisAvailability = analysisAvailability;
    }

    public bool IsCaptureMemorySearchEnabled =>
        _analysisAvailability.IsCaptureAnalysisEnabled &&
        _featureManager.IsEnabled(AppFeatures.Feature_CaptureMemory_Search);
}
