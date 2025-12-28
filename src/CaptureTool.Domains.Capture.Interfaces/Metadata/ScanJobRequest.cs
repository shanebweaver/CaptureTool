using System.Text.Json.Serialization;

namespace CaptureTool.Domains.Capture.Interfaces.Metadata;

/// <summary>
/// Represents a persistent scan job request stored in the job queue folder.
/// </summary>
public sealed class ScanJobRequest
{
    /// <summary>
    /// Gets the unique identifier for this scan job.
    /// </summary>
    [JsonPropertyName("jobId")]
    public Guid JobId { get; init; }

    /// <summary>
    /// Gets the path to the media file to scan.
    /// </summary>
    [JsonPropertyName("mediaFilePath")]
    public string MediaFilePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the job was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets optional scan criteria or scanner IDs to use.
    /// </summary>
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
