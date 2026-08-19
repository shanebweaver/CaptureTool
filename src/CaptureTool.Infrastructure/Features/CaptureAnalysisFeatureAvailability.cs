using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Domain.Analysis;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class CaptureAnalysisFeatureAvailability : ICaptureAnalysisFeatureAvailability
{
    private const long CurrentResolutionPolicyRevision = 5;
    private const string MicrosoftWindowsProviderId = "microsoft-windows";
    private const string MicrosoftFoundryLocalProviderId = "microsoft-foundry-local";

    private readonly IFeatureManager _featureManager;
    private readonly ICaptureAnalyzerSelectionService? _selection;

    public CaptureAnalysisFeatureAvailability(
        IFeatureManager featureManager,
        ICaptureAnalyzerSelectionService? selection = null)
    {
        ArgumentNullException.ThrowIfNull(featureManager);
        _featureManager = featureManager;
        _selection = selection;
    }

    public bool IsCaptureAnalysisEnabled =>
        _featureManager.IsEnabled(AppFeatures.Feature_CaptureAnalysis_Platform);

    public long ResolutionPolicyRevision => _selection is not { Revision: > 0 } selection
        ? CurrentResolutionPolicyRevision
        : checked((CurrentResolutionPolicyRevision * 1_000_000_000L) + selection.Revision);

    public bool IsProviderEnabled(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        return IsCaptureAnalysisEnabled && providerId switch
        {
            MicrosoftWindowsProviderId => _featureManager.IsEnabled(
                AppFeatures.Feature_CaptureAnalysis_Provider_MicrosoftWindows),
            MicrosoftFoundryLocalProviderId => _featureManager.IsEnabled(
                AppFeatures.Feature_CaptureAnalysis_Provider_MicrosoftFoundryLocal),
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

        bool? selectionOverride = _selection?.GetFeatureEnabledOverride(analyzer);
        if (selectionOverride.HasValue)
        {
            return selectionOverride.Value;
        }

        return analyzer.AnalyzerId switch
        {
            "windows-image-media-properties" => _featureManager.IsEnabled(
                AppFeatures.Feature_CaptureAnalysis_Analyzer_WindowsImageMediaProperties),
            "windows-ocr-document" => _featureManager.IsEnabled(
                AppFeatures.Feature_CaptureAnalysis_Analyzer_WindowsOcrDocument),
            "windows-ai-ocr-document" => _featureManager.IsEnabled(
                AppFeatures.Feature_CaptureAnalysis_Analyzer_WindowsAiOcrDocument),
            "windows-image-description" => _featureManager.IsEnabled(
                AppFeatures.Feature_CaptureAnalysis_Analyzer_WindowsImageDescription),
            "windows-video-frame-ocr" => _featureManager.IsEnabled(
                AppFeatures.Feature_CaptureAnalysis_Analyzer_WindowsVideoFrameOcr),
            "windows-ai-video-frame-ocr" => _featureManager.IsEnabled(
                AppFeatures.Feature_CaptureAnalysis_Analyzer_WindowsAiVideoFrameOcr),
            "windows-video-frame-description" => _featureManager.IsEnabled(
                AppFeatures.Feature_CaptureAnalysis_Analyzer_WindowsVideoFrameDescription),
            "windows-ai-speech-transcript" => _featureManager.IsEnabled(
                AppFeatures.Feature_CaptureAnalysis_Analyzer_WindowsAiSpeechTranscript),
            "foundry-local-speech-transcript" => _featureManager.IsEnabled(
                AppFeatures.Feature_CaptureAnalysis_Analyzer_FoundryLocalSpeechTranscript),
            _ => false,
        };
    }
}
