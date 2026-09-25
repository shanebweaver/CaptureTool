using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Domain.Analysis;

/// <summary>A UTF-16 span in an OCR region, QR entry, description, or transcript segment.</summary>
public sealed record AnalysisEvidence
{
    public Guid ResultId { get; }
    public int EntryIndex { get; }
    public int Start { get; }
    public int Length { get; }

    public AnalysisEvidence(Guid resultId, int entryIndex, int start, int length)
    {
        if (resultId == Guid.Empty) throw new ArgumentException("Source result identity is required.", nameof(resultId));
        if (entryIndex < 0) throw new ArgumentOutOfRangeException(nameof(entryIndex));
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
        ResultId = resultId;
        EntryIndex = entryIndex;
        Start = start;
        Length = length;
    }

    public string ResolveText(AnalysisResult source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.ResultId != ResultId) throw new ArgumentException("Evidence belongs to another result.", nameof(source));
        string text = source.Payload switch
        {
            TextRecognitionMetadata ocr when EntryIndex < ocr.Regions.Count => ocr.Regions[EntryIndex].Text,
            QrCodeMetadata qr when EntryIndex < qr.Codes.Count => qr.Codes[EntryIndex].Value,
            DescriptionMetadata descriptions when EntryIndex < descriptions.Descriptions.Count => descriptions.Descriptions[EntryIndex].Text,
            TranscriptMetadata transcript when EntryIndex < transcript.Segments.Count => transcript.Segments[EntryIndex].Text,
            _ => throw new ArgumentException("Evidence entry does not exist or has no text.", nameof(source)),
        };
        if (Start > text.Length || Length > text.Length - Start ||
            SplitsSurrogate(text, Start) || SplitsSurrogate(text, Start + Length))
            throw new ArgumentException("Evidence span is outside the source text or splits a character.", nameof(source));
        return text.Substring(Start, Length);
    }

    private static bool SplitsSurrogate(string text, int offset) => offset > 0 && offset < text.Length &&
        char.IsHighSurrogate(text[offset - 1]) && char.IsLowSurrogate(text[offset]);
}
