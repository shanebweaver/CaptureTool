using System.Text.Json.Serialization;

namespace CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Processing;

/// <summary>
/// DTO for serializing refined metadata files to JSON (AOT-compatible).
/// </summary>
public sealed class RefinedMetadataFileDto
{
    [JsonPropertyName("sourceFilePath")]
    public string SourceFilePath { get; set; } = string.Empty;

    [JsonPropertyName("sourceMetadataFilePath")]
    public string? SourceMetadataFilePath { get; set; }

    [JsonPropertyName("processingTimestamp")]
    public DateTime ProcessingTimestamp { get; set; }

    [JsonPropertyName("processorInfo")]
    public Dictionary<string, string> ProcessorInfo { get; set; } = new();

    [JsonPropertyName("insights")]
    public List<InsightEntryDto> Insights { get; set; } = [];
}

/// <summary>
/// DTO for serializing insight entries to JSON (AOT-compatible).
/// </summary>
public sealed class InsightEntryDto
{
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("duration")]
    public long? Duration { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("processorId")]
    public string ProcessorId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("sourceEntryIds")]
    public List<string> SourceEntryIds { get; set; } = [];

    [JsonPropertyName("additionalData")]
    public Dictionary<string, string>? AdditionalData { get; set; }
}
