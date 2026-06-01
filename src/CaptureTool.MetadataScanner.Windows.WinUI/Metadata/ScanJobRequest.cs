using System.Text.Json.Serialization;

namespace CaptureTool.MetadataScanner.Windows.WinUI.Metadata;

public sealed class ScanJobRequest
{
    [JsonPropertyName("jobId")]
    public Guid JobId { get; init; }

    [JsonPropertyName("mediaFilePath")]
    public string MediaFilePath { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("scanCriteria")]
    public Dictionary<string, object>? ScanCriteria { get; init; }

    public ScanJobRequest()
    {
    }

    public ScanJobRequest(Guid jobId, string mediaFilePath, Dictionary<string, object>? scanCriteria = null)
    {
        JobId = jobId;
        MediaFilePath = mediaFilePath ?? throw new ArgumentNullException(nameof(mediaFilePath));
        CreatedAt = DateTime.UtcNow;
        ScanCriteria = scanCriteria;
    }
}
