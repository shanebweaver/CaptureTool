using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Domain.Analysis;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class CaptureAnalysisFeatureAvailability : ICaptureAnalysisFeatureAvailability
{
    private const long CurrentResolutionPolicyRevision = 2;
    private const string MicrosoftWindowsProviderId = "microsoft-windows";

    private readonly IFeatureManager _featureManager;

    public CaptureAnalysisFeatureAvailability(IFeatureManager featureManager)
    {
        ArgumentNullException.ThrowIfNull(featureManager);
        _featureManager = featureManager;
    }

    public bool IsCaptureAnalysisEnabled =>
        _featureManager.IsEnabled(AppFeatures.Feature_CaptureAnalysis_Platform);

    public long ResolutionPolicyRevision => CurrentResolutionPolicyRevision;

    public bool IsProviderEnabled(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        return IsCaptureAnalysisEnabled && providerId switch
        {
            MicrosoftWindowsProviderId => _featureManager.IsEnabled(
                AppFeatures.Feature_CaptureAnalysis_Provider_MicrosoftWindows),
            _ => false,
        };
    }

    public bool IsAnalyzerEnabled(AnalyzerIdentity analyzer)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        if (!IsProviderEnabled(analyzer.ProviderId))
        {
            return false;
        }

        return analyzer.AnalyzerId switch
        {
            "windows-image-media-properties" => _featureManager.IsEnabled(
                AppFeatures.Feature_CaptureAnalysis_Analyzer_WindowsImageMediaProperties),
            "windows-ocr-document" => _featureManager.IsEnabled(
                AppFeatures.Feature_CaptureAnalysis_Analyzer_WindowsOcrDocument),
            "windows-image-description" => _featureManager.IsEnabled(
                AppFeatures.Feature_CaptureAnalysis_Analyzer_WindowsImageDescription),
            _ => false,
        };
    }
}
