using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Library.CaptureDetails;

/// <summary>Controls details-pane tabs without changing analysis capabilities or stored results.</summary>
public interface ICaptureDetailsFeatureAvailability
{
    bool IsTextTabEnabled(AnalysisMediaKind mediaKind);
    bool IsSpeechTabEnabled(AnalysisMediaKind mediaKind);
}
