namespace CaptureTool.Domain.Analysis.Payloads;

public sealed class TextRecognitionMetadata : AnalysisPayload
{
    public override AnalysisCapability Capability => AnalysisCapability.TextRecognition;
    public IReadOnlyList<RecognizedText> Regions { get; }

    public TextRecognitionMetadata(IEnumerable<RecognizedText> regions) => Regions = AnalysisGuard.Freeze(regions);
    public override bool Supports(AnalysisMediaKind mediaKind) => mediaKind is AnalysisMediaKind.Image or AnalysisMediaKind.Video;
}
