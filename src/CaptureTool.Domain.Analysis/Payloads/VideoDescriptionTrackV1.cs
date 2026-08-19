using System.Text;

namespace CaptureTool.Domain.Analysis.Payloads;

public sealed record VideoDescriptionObservationV1
{
    public const int MaximumDescriptionLength = 8_192;

    public VideoDescriptionObservationV1(
        string description,
        TimeSpan startTime,
        TimeSpan endTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        string normalized = description
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim()
            .Normalize(NormalizationForm.FormC);
        if (normalized.Length > MaximumDescriptionLength)
        {
            throw new ArgumentException(
                $"A video description cannot exceed {MaximumDescriptionLength} characters.",
                nameof(description));
        }

        if (startTime < TimeSpan.Zero || endTime <= startTime)
        {
            throw new ArgumentException(
                "A video description requires a positive, ordered time range.",
                nameof(startTime));
        }

        Description = normalized;
        StartTime = startTime;
        EndTime = endTime;
    }

    public string Description { get; }

    public TimeSpan StartTime { get; }

    public TimeSpan EndTime { get; }
}

public sealed class VideoDescriptionTrackV1 : CapabilityPayload
{
    public const int MaximumFullTextLength = 500_000;
    public const int MaximumObservationCount = 1_000;

    public VideoDescriptionTrackV1(
        string fullText,
        IEnumerable<VideoDescriptionObservationV1>? observations = null)
    {
        ArgumentNullException.ThrowIfNull(fullText);
        string normalized = fullText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim()
            .Normalize(NormalizationForm.FormC);
        if (normalized.Length > MaximumFullTextLength)
        {
            throw new ArgumentException(
                $"A video description track cannot exceed {MaximumFullTextLength} characters.",
                nameof(fullText));
        }

        VideoDescriptionObservationV1[] copied = observations == null ? [] : [.. observations];
        if (copied.Length > MaximumObservationCount)
        {
            throw new ArgumentException(
                $"A video description track cannot exceed {MaximumObservationCount} observations.",
                nameof(observations));
        }

        if (copied.Any(observation => observation == null))
        {
            throw new ArgumentException(
                "Video description observations cannot contain null values.",
                nameof(observations));
        }

        for (int index = 1; index < copied.Length; index++)
        {
            if (copied[index].StartTime < copied[index - 1].EndTime)
            {
                throw new ArgumentException(
                    "Video description observations must be chronological and non-overlapping.",
                    nameof(observations));
            }
        }

        FullText = normalized;
        Observations = Array.AsReadOnly(copied);
    }

    public override CapabilityDefinition Definition => AnalysisCapabilities.VideoDescriptionTrackV1;

    public string FullText { get; }

    public IReadOnlyList<VideoDescriptionObservationV1> Observations { get; }

    public override bool IsEquivalentTo(CapabilityPayload other)
    {
        return other is VideoDescriptionTrackV1 track &&
            string.Equals(FullText, track.FullText, StringComparison.Ordinal) &&
            Observations.SequenceEqual(track.Observations);
    }
}
