namespace CaptureTool.Domain.Capture.Windows.Metadata.Processing;

internal sealed class InsightEntryDto
{
    public long Timestamp { get; set; }

    public long? Duration { get; set; }

    public string Category { get; set; } = string.Empty;

    public string ProcessorId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<string> Tags { get; set; } = [];

    public double Confidence { get; set; }

    public List<string> SourceEntryIds { get; set; } = [];

    public Dictionary<string, string>? AdditionalData { get; set; }
}
