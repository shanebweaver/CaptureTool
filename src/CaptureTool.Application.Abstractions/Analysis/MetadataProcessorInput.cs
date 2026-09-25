using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Abstractions.Analysis;

public sealed record MetadataTextEntry(Guid ResultId, AnalysisCapability Capability, int EntryIndex, string Text);

/// <summary>Whole text entries only: truncating a token could invent a fact. Original entry indexes are preserved.</summary>
public sealed class MetadataProcessorInput
{
    public CaptureId CaptureId { get; }
    public AnalysisMediaKind MediaKind { get; }
    public SourceRevision SourceRevision { get; }
    public MetadataProcessorDescriptor Descriptor { get; }
    public IReadOnlyList<AnalysisInputReference> Inputs { get; }
    public AnalysisDerivation? Derivation { get; }
    public IReadOnlyList<MetadataTextEntry> Entries { get; }
    public MetadataProcessingCoverage Coverage { get; }

    private MetadataProcessorInput(CaptureAnalysisRecord record, MetadataProcessorDescriptor descriptor,
        AnalysisInputReference[] inputs, List<MetadataTextEntry> entries, MetadataProcessingCoverage coverage)
    {
        CaptureId = record.CaptureId;
        MediaKind = record.MediaKind;
        SourceRevision = record.SourceRevision;
        Descriptor = descriptor;
        Inputs = Array.AsReadOnly(inputs);
        Derivation = inputs.Any(input => input.ResultId != null) ? new(inputs) : null;
        Entries = entries.AsReadOnly();
        Coverage = coverage;
    }

    public static MetadataProcessorInput Create(CaptureAnalysisRecord record, MetadataProcessorDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(descriptor);
        cancellationToken.ThrowIfCancellationRequested();
        AnalysisResult?[] sources = descriptor.Inputs.Select(capability =>
            record.Results.SingleOrDefault(result => result.Payload.Capability == capability)).ToArray();
        AnalysisInputReference[] references = descriptor.Inputs.Select((capability, index) => new AnalysisInputReference(capability, sources[index]?.ResultId)).ToArray();
        long available = sources.Sum(source => (long)EntryCount(source?.Payload));
        MetadataProcessingLimit limited = MetadataProcessingLimit.None;
        MetadataProcessingLimits limits = descriptor.Limits;
        List<MetadataTextEntry> entries = [];
        int examined = 0;
        int characters = 0;
        foreach (AnalysisResult source in sources.OfType<AnalysisResult>())
        {
            int count = EntryCount(source.Payload);
            for (int index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (examined == limits.MaxEntries)
                {
                    limited |= MetadataProcessingLimit.InputEntries;
                    return Complete();
                }
                examined++;
                string text = EntryText(source.Payload, index);
                if (text.Length > limits.MaxEntryCharacters)
                {
                    limited |= MetadataProcessingLimit.OversizedEntry;
                    continue;
                }
                if (text.Length > limits.MaxCharacters - characters)
                {
                    limited |= MetadataProcessingLimit.InputCharacters;
                    return Complete();
                }
                entries.Add(new(source.ResultId, source.Payload.Capability, index, text));
                characters += text.Length;
            }
        }
        return Complete();

        MetadataProcessorInput Complete()
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(record, descriptor, references, entries, new(available, entries.Count, characters, limited));
        }
    }

    private static int EntryCount(AnalysisPayload? payload) => payload switch
    {
        null => 0,
        TextRecognitionMetadata ocr => ocr.Regions.Count,
        QrCodeMetadata qr => qr.Codes.Count,
        TranscriptMetadata transcript => transcript.Segments.Count,
        DescriptionMetadata descriptions => descriptions.Descriptions.Count,
        _ => throw new ArgumentException("Unsupported metadata input payload.", nameof(payload)),
    };

    private static string EntryText(AnalysisPayload payload, int index) => payload switch
    {
        TextRecognitionMetadata ocr => ocr.Regions[index].Text,
        QrCodeMetadata qr => qr.Codes[index].Value,
        TranscriptMetadata transcript => transcript.Segments[index].Text,
        DescriptionMetadata descriptions => descriptions.Descriptions[index].Text,
        _ => throw new ArgumentException("Unsupported metadata input payload.", nameof(payload)),
    };
}
