using System.Text;

namespace CaptureTool.Domain.Analysis.Payloads;

public sealed record VideoOcrObservationV1
{
    public const int MaximumTextLength = 65_536;

    public VideoOcrObservationV1(
        string text,
        TimeSpan startTime,
        TimeSpan endTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        string normalizedText = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim()
            .Normalize(NormalizationForm.FormC);
        if (normalizedText.Length > MaximumTextLength)
        {
            throw new ArgumentException(
                $"A video OCR observation cannot exceed {MaximumTextLength} characters.",
                nameof(text));
        }

        if (startTime < TimeSpan.Zero || endTime <= startTime)
        {
            throw new ArgumentException(
                "A video OCR observation requires a positive, ordered time range.",
                nameof(startTime));
        }

        Text = normalizedText;
        StartTime = startTime;
        EndTime = endTime;
    }

    public string Text { get; }

    public TimeSpan StartTime { get; }

    public TimeSpan EndTime { get; }
}

public sealed class VideoOcrTrackV1 : CapabilityPayload
{
    public const int MaximumFullTextLength = 1_000_000;
    public const int MaximumObservationCount = 50_000;

    public VideoOcrTrackV1(
        string fullText,
        IEnumerable<VideoOcrObservationV1>? observations = null)
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
                $"A video OCR track cannot exceed {MaximumFullTextLength} characters.",
                nameof(fullText));
        }

        VideoOcrObservationV1[] copied = observations == null ? [] : [.. observations];
        if (copied.Length > MaximumObservationCount)
        {
            throw new ArgumentException(
                $"A video OCR track cannot exceed {MaximumObservationCount} observations.",
                nameof(observations));
        }

        if (copied.Any(observation => observation == null))
        {
            throw new ArgumentException(
                "Video OCR observations cannot contain null values.",
                nameof(observations));
        }

        for (int index = 1; index < copied.Length; index++)
        {
            if (copied[index].StartTime < copied[index - 1].EndTime)
            {
                throw new ArgumentException(
                    "Video OCR observations must be chronological and non-overlapping.",
                    nameof(observations));
            }
        }

        FullText = normalizedText;
        Observations = Array.AsReadOnly(copied);
    }

    public override CapabilityDefinition Definition => AnalysisCapabilities.VideoOcrTrackV1;

    public string FullText { get; }

    public IReadOnlyList<VideoOcrObservationV1> Observations { get; }

    public override bool IsEquivalentTo(CapabilityPayload other)
    {
        return other is VideoOcrTrackV1 track &&
            string.Equals(FullText, track.FullText, StringComparison.Ordinal) &&
            Observations.SequenceEqual(track.Observations);
    }
}
