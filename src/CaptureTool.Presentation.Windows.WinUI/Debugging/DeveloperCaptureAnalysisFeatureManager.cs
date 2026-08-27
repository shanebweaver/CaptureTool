#if DEBUG
using CaptureTool.FeatureManagement;

namespace CaptureTool.Presentation.Windows.WinUI.Debugging;

internal sealed class DeveloperCaptureAnalysisFeatureManager : IFeatureManager
{
    private readonly MicrosoftFeatureManager _releaseDefaults = new();

    public bool IsEnabled(FeatureFlag featureFlag)
    {
        return ReferenceEquals(featureFlag, AppFeatures.Feature_CaptureAnalysis_Platform) ||
            ReferenceEquals(featureFlag, AppFeatures.Feature_CaptureMemory_Search) ||
            _releaseDefaults.IsEnabled(featureFlag);
    }
}
#endif
