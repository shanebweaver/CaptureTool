namespace CaptureTool.Domain.Analysis.Payloads;

/// <summary>A suggested accessible image description, grounded in saved visual and OCR evidence.</summary>
public sealed class ImageAltTextMetadata : DerivedAnalysisPayload
{
    public override AnalysisCapability Capability => AnalysisCapability.ImageAltText;
    public SuggestedText? Suggestion { get; }
    public MetadataProcessingCoverage Coverage { get; }

    public ImageAltTextMetadata(SuggestedText? suggestion, MetadataProcessingCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        Suggestion = suggestion;
        Coverage = coverage;
    }

    public override bool Supports(AnalysisMediaKind mediaKind) => mediaKind == AnalysisMediaKind.Image;
    public override void ValidateEvidence(IReadOnlyList<AnalysisResult> inputs) =>
        InsightValidation.Validate(inputs, Coverage, Suggestion?.Evidence ?? []);
}
