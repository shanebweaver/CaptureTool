using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Domain.Analysis;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class CaptureAnalysisFeatureAvailability : ICaptureAnalysisFeatureAvailability
{
    private const long CurrentResolutionPolicyRevision = 1;

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
        return IsCaptureAnalysisEnabled;
    }

    public bool IsAnalyzerEnabled(AnalyzerIdentity analyzer)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        return IsProviderEnabled(analyzer.ProviderId);
    }
}
