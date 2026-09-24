namespace CaptureTool.Domain.Analysis.Payloads;

public sealed record MediaDescription
{
    public string Text { get; }
    public TimeSpan? Timestamp { get; }

    public MediaDescription(string text, TimeSpan? timestamp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (timestamp < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timestamp));
        Text = text;
        Timestamp = timestamp;
    }
}

public sealed class DescriptionMetadata : AnalysisPayload
{
    public override AnalysisCapability Capability => AnalysisCapability.Description;
    public IReadOnlyList<MediaDescription> Descriptions { get; }

    public DescriptionMetadata(IEnumerable<MediaDescription> descriptions) => Descriptions = AnalysisGuard.Freeze(descriptions);
    public override bool Supports(AnalysisMediaKind mediaKind) => mediaKind is AnalysisMediaKind.Image or AnalysisMediaKind.Video;
}
