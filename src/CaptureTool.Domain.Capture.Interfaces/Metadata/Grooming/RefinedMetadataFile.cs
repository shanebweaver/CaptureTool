namespace CaptureTool.Domain.Capture.Interfaces.Metadata.Grooming;

/// <summary>
/// Represents a refined metadata file containing high-level insights derived from raw scan results.
/// Designed to be easily consumed by a playback UI for an enhanced viewing experience.
/// </summary>
public sealed class RefinedMetadataFile
{
    /// <summary>
    /// The file extension used for refined metadata files.
    /// </summary>
    public const string FileExtension = ".insights.json";

    /// <summary>
    /// Gets the path to the original media file.
    /// </summary>
    public string SourceFilePath { get; }

    /// <summary>
    /// Gets the path to the raw metadata file this was derived from.
    /// May be null or empty when the refined metadata was produced in-memory
    /// (i.e., via <see cref="IMetadataGroomingPipeline.ProcessAsync"/>)
    /// rather than saved to disk.
    /// </summary>
    public string? SourceMetadataFilePath { get; }

    /// <summary>
    /// Gets the timestamp when grooming was performed.
    /// </summary>
    public DateTime GroomingTimestamp { get; }

    /// <summary>
    /// Gets information about the groomers used, keyed by groomer ID.
    /// </summary>
    public IReadOnlyDictionary<string, string> GroomerInfo { get; }

    /// <summary>
    /// Gets the list of refined insights, ordered by timestamp.
    /// </summary>
    public IReadOnlyList<InsightEntry> Insights { get; }

    public RefinedMetadataFile(
        string sourceFilePath,
        string? sourceMetadataFilePath,
        DateTime groomingTimestamp,
        IReadOnlyList<InsightEntry> insights,
        IReadOnlyDictionary<string, string> groomerInfo)
    {
        SourceFilePath = sourceFilePath ?? throw new ArgumentNullException(nameof(sourceFilePath));
        SourceMetadataFilePath = sourceMetadataFilePath;
        GroomingTimestamp = groomingTimestamp;
        Insights = insights ?? throw new ArgumentNullException(nameof(insights));
        GroomerInfo = groomerInfo ?? throw new ArgumentNullException(nameof(groomerInfo));
    }
}
