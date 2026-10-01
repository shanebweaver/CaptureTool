using CaptureTool.Application.Abstractions.Library.CaptureDetails;
using CaptureTool.Domain.Analysis;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class CaptureDetailsFeatureAvailability(IFeatureManager featureManager) : ICaptureDetailsFeatureAvailability
{
    public bool IsTextTabEnabled(AnalysisMediaKind mediaKind) => mediaKind switch
    {
        AnalysisMediaKind.Image => true,
        AnalysisMediaKind.Video => featureManager.IsEnabled(AppFeatures.Feature_CaptureDetails_VideoTextTab),
        _ => false
    };

    public bool IsSpeechTabEnabled(AnalysisMediaKind mediaKind) => mediaKind switch
    {
        AnalysisMediaKind.Video => featureManager.IsEnabled(AppFeatures.Feature_CaptureDetails_VideoSpeechTab),
        AnalysisMediaKind.Audio => featureManager.IsEnabled(AppFeatures.Feature_CaptureDetails_AudioSpeechTab),
        _ => false
    };
}
