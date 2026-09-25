using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Analysis;

public sealed record StructuredFactsOptions
{
    public MetadataProcessingLimits Limits { get; }
    public int MaxFacts { get; }
    public int MaxEvidencePerFact { get; }

    public StructuredFactsOptions(MetadataProcessingLimits limits, int maxFacts, int maxEvidencePerFact)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (maxFacts is < 1 or > StructuredFactsMetadata.MaximumFactCount) throw new ArgumentOutOfRangeException(nameof(maxFacts));
        if (maxEvidencePerFact is < 1 or > StructuredFact.MaximumEvidenceCount) throw new ArgumentOutOfRangeException(nameof(maxEvidencePerFact));
        Limits = limits;
        MaxFacts = maxFacts;
        MaxEvidencePerFact = maxEvidencePerFact;
    }
}

/// <summary>Metadata processor contracts and budgets. Worker registration remains a separate integration slice.</summary>
public static class MetadataEnrichmentConfiguration
{
    public static StructuredFactsOptions StructuredFacts { get; } = new(new(2048, 131072, 32768, TimeSpan.FromSeconds(2)), 256, 16);

    public static MetadataProcessorDescriptor CreateStructuredFacts(MetadataProcessingLimits limits) =>
        new("local-structured-facts", "1", AnalysisCapability.StructuredFacts,
            [AnalysisCapability.TextRecognition, AnalysisCapability.QrCodeDetection, AnalysisCapability.Transcription],
            limits);
}
