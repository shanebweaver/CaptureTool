namespace CaptureTool.Domain.Capture.Windows.Metadata.Processing;

internal sealed class RefinedMetadataFileDto
{
    public string SourceFilePath { get; set; } = string.Empty;

    public string? SourceMetadataFilePath { get; set; }

    public DateTime ProcessingTimestamp { get; set; }

    public Dictionary<string, string> ProcessorInfo { get; set; } = [];

    public List<InsightEntryDto> Insights { get; set; } = [];
}
