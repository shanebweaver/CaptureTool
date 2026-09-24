namespace CaptureTool.Domain.Analysis.Payloads;

public sealed record TranscriptSegment
{
    public string Text { get; }
    public TimeSpan Start { get; }
    public TimeSpan End { get; }

    public TranscriptSegment(string text, TimeSpan start, TimeSpan end)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (start < TimeSpan.Zero || end < start) throw new ArgumentOutOfRangeException(nameof(end));
        Text = text;
        Start = start;
        End = end;
    }
}

public sealed class TranscriptMetadata : AnalysisPayload
{
    public override AnalysisCapability Capability => AnalysisCapability.Transcription;
    public string? Language { get; }
    public IReadOnlyList<TranscriptSegment> Segments { get; }

    public TranscriptMetadata(string? language, IEnumerable<TranscriptSegment> segments)
    {
        Language = language == null ? null : AnalysisGuard.Identifier(language, nameof(language));
        Segments = AnalysisGuard.Freeze(segments);
        if (Segments.Zip(Segments.Skip(1)).Any(pair => pair.First.Start > pair.Second.Start))
            throw new ArgumentException("Transcript segments must be ordered by start time.", nameof(segments));
    }

    public override bool Supports(AnalysisMediaKind mediaKind) => mediaKind is AnalysisMediaKind.Audio or AnalysisMediaKind.Video;
}
