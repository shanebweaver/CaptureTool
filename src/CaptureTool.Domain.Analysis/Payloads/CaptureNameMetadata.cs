namespace CaptureTool.Domain.Analysis.Payloads;

/// <summary>A suggested name, independent of the summary and any user-accepted filename.</summary>
public sealed class CaptureNameMetadata : DerivedAnalysisPayload
{
    public override AnalysisCapability Capability => AnalysisCapability.CaptureName;
    public SuggestedText? Suggestion { get; }
    public MetadataProcessingCoverage Coverage { get; }

    public CaptureNameMetadata(SuggestedText? suggestion, MetadataProcessingCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        if (suggestion?.Text.Length > 160)
        {
            throw new ArgumentException("Suggested name is too long.", nameof(suggestion));
        }

        Suggestion = suggestion;
        Coverage = coverage;
    }

    public override bool Supports(AnalysisMediaKind mediaKind) => Enum.IsDefined(mediaKind);
    public override void ValidateEvidence(IReadOnlyList<AnalysisResult> inputs) =>
        InsightValidation.Validate(inputs, Coverage, Suggestion?.Evidence ?? []);
}
