using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Domain.Analysis;

[Flags]
public enum MetadataProcessingLimit
{
    None = 0,
    InputEntries = 1,
    InputCharacters = 2,
    OversizedEntry = 4,
    Facts = 8,
    Evidence = 16,
    FactValue = 32,
}

/// <summary>Coverage of available, declared metadata; never a claim about the entire original media.</summary>
public sealed record MetadataProcessingCoverage
{
    public long AvailableEntries { get; }
    public int IncludedEntries { get; }
    public int IncludedCharacters { get; }
    public MetadataProcessingLimit Limits { get; }
    public bool IsComplete => Limits == MetadataProcessingLimit.None;

    public MetadataProcessingCoverage(long availableEntries, int includedEntries, int includedCharacters, MetadataProcessingLimit limits)
    {
        const MetadataProcessingLimit known = MetadataProcessingLimit.InputEntries | MetadataProcessingLimit.InputCharacters |
            MetadataProcessingLimit.OversizedEntry | MetadataProcessingLimit.Facts | MetadataProcessingLimit.Evidence | MetadataProcessingLimit.FactValue;
        const MetadataProcessingLimit inputLimits = MetadataProcessingLimit.InputEntries | MetadataProcessingLimit.InputCharacters | MetadataProcessingLimit.OversizedEntry;
        if (availableEntries < 0 || includedEntries < 0 || includedEntries > availableEntries || includedCharacters < includedEntries ||
            includedEntries == 0 && includedCharacters != 0)
            throw new ArgumentException("Invalid metadata coverage counts.");
        if ((limits & ~known) != 0 || (includedEntries < availableEntries) != ((limits & inputLimits) != 0))
            throw new ArgumentException("Coverage limits must describe omitted input entries.", nameof(limits));
        AvailableEntries = availableEntries;
        IncludedEntries = includedEntries;
        IncludedCharacters = includedCharacters;
        Limits = limits;
    }

    public MetadataProcessingCoverage WithOutputLimits(MetadataProcessingLimit limits)
    {
        const MetadataProcessingLimit outputLimits = MetadataProcessingLimit.Facts | MetadataProcessingLimit.Evidence | MetadataProcessingLimit.FactValue;
        if ((limits & ~outputLimits) != 0) throw new ArgumentException("Only output limits may be added after input selection.", nameof(limits));
        return new(AvailableEntries, IncludedEntries, IncludedCharacters, Limits | limits);
    }

    internal void Validate(IReadOnlyList<AnalysisResult> inputs, IEnumerable<AnalysisEvidence> evidence)
    {
        long available = inputs.Sum(input => input.Payload switch
        {
            TextRecognitionMetadata ocr => (long)ocr.Regions.Count,
            QrCodeMetadata qr => qr.Codes.Count,
            TranscriptMetadata transcript => transcript.Segments.Count,
            DescriptionMetadata descriptions => descriptions.Descriptions.Count,
            _ => 0,
        });
        var entries = evidence.GroupBy(span => (span.ResultId, span.EntryIndex)).ToArray();
        if (AvailableEntries != available || entries.Length > IncludedEntries ||
            entries.Sum(entry => entry.Max(span => (long)span.Start + span.Length)) > IncludedCharacters)
            throw new ArgumentException("Coverage does not match declared sources and evidence.", nameof(inputs));
        if (IncludedEntries == available)
        {
            long characters = inputs.Sum(input => input.Payload switch
            {
                TextRecognitionMetadata ocr => ocr.Regions.Sum(region => (long)region.Text.Length),
                QrCodeMetadata qr => qr.Codes.Sum(code => (long)code.Value.Length),
                TranscriptMetadata transcript => transcript.Segments.Sum(segment => (long)segment.Text.Length),
                DescriptionMetadata descriptions => descriptions.Descriptions.Sum(description => (long)description.Text.Length),
                _ => 0,
            });
            if (IncludedCharacters != characters)
                throw new ArgumentException("Complete input coverage must account for all source characters.", nameof(inputs));
        }
    }
}
