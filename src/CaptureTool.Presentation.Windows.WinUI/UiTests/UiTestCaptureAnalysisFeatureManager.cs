using CaptureTool.FeatureManagement;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestCaptureAnalysisFeatureManager : IFeatureManager
{
    private readonly MicrosoftFeatureManager _defaults = new();

    public bool IsEnabled(FeatureFlag featureFlag)
    {
        return ReferenceEquals(featureFlag, AppFeatures.Feature_CaptureAnalysis_Platform) ||
            ReferenceEquals(featureFlag, AppFeatures.Feature_CaptureMemory_Search) ||
            _defaults.IsEnabled(featureFlag);
    }
}
