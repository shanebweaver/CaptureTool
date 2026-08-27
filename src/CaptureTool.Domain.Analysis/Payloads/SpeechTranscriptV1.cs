using System.Text;

namespace CaptureTool.Domain.Analysis.Payloads;

public sealed record SpeechTranscriptSegmentV1
{
    public const int MaximumTextLength = 65_536;

    public SpeechTranscriptSegmentV1(
        string text,
        TimeSpan? startTime = null,
        TimeSpan? endTime = null,
        string? speakerLabel = null,
        double? confidence = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        string normalizedText = text.Trim().Normalize(NormalizationForm.FormC);
        if (normalizedText.Length > MaximumTextLength)
        {
            throw new ArgumentException(
                $"A transcript segment cannot exceed {MaximumTextLength} characters.",
                nameof(text));
        }

        if (startTime.HasValue != endTime.HasValue ||
            startTime < TimeSpan.Zero ||
            endTime < startTime)
        {
            throw new ArgumentException(
                "Transcript segment timing must be absent or a non-negative ordered range.",
                nameof(startTime));
        }

        Text = normalizedText;
        StartTime = startTime;
        EndTime = endTime;
        SpeakerLabel = PayloadValidation.NormalizeOptional(
            speakerLabel,
            nameof(speakerLabel),
            maximumLength: 128);
        Confidence = PayloadValidation.ValidateConfidence(confidence, nameof(confidence));
    }

    public string Text { get; }

    public TimeSpan? StartTime { get; }

    public TimeSpan? EndTime { get; }

    public string? SpeakerLabel { get; }

    public double? Confidence { get; }
}

public sealed class SpeechTranscriptV1 : CapabilityPayload
{
    public const int MaximumFullTextLength = 1_000_000;
    public const int MaximumSegmentCount = 50_000;

    public SpeechTranscriptV1(
        string fullText,
        IEnumerable<SpeechTranscriptSegmentV1>? segments = null,
        string? languageTag = null)
    {
        ArgumentNullException.ThrowIfNull(fullText);
        string normalizedText = fullText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim()
            .Normalize(NormalizationForm.FormC);
        if (normalizedText.Length > MaximumFullTextLength)
        {
            throw new ArgumentException(
                $"A speech transcript cannot exceed {MaximumFullTextLength} characters.",
                nameof(fullText));
        }

        SpeechTranscriptSegmentV1[] copiedSegments = segments == null ? [] : [.. segments];
        if (copiedSegments.Length > MaximumSegmentCount)
        {
            throw new ArgumentException(
                $"A speech transcript cannot exceed {MaximumSegmentCount} segments.",
                nameof(segments));
        }

        if (copiedSegments.Any(segment => segment == null))
        {
            throw new ArgumentException("Transcript segments cannot contain null values.", nameof(segments));
        }

        FullText = normalizedText;
        Segments = Array.AsReadOnly(copiedSegments);
        LanguageTag = PayloadValidation.NormalizeOptional(
            languageTag,
            nameof(languageTag),
            maximumLength: 64);
    }

    public override CapabilityDefinition Definition => AnalysisCapabilities.SpeechTranscriptV1;

    public string FullText { get; }

    public IReadOnlyList<SpeechTranscriptSegmentV1> Segments { get; }

    public string? LanguageTag { get; }

    public override bool IsEquivalentTo(CapabilityPayload other)
    {
        return other is SpeechTranscriptV1 transcript &&
            string.Equals(FullText, transcript.FullText, StringComparison.Ordinal) &&
            string.Equals(LanguageTag, transcript.LanguageTag, StringComparison.Ordinal) &&
            Segments.SequenceEqual(transcript.Segments);
    }
}
