namespace CaptureTool.Domain.Capture.Interfaces.Metadata.Grooming;

/// <summary>
/// Configurable pipeline that runs registered groomers over raw metadata
/// and produces a <see cref="RefinedMetadataFile"/> suitable for enhanced playback.
/// </summary>
public interface IMetadataGroomingPipeline
{
    /// <summary>
    /// Runs all registered groomers over the provided raw metadata file and
    /// returns a refined metadata file with timestamped insights.
    /// </summary>
    /// <param name="rawMetadata">The raw metadata produced by Layer 1 scanners.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="RefinedMetadataFile"/> containing all insights produced by the pipeline,
    /// or null if no groomers are registered.
    /// </returns>
    Task<RefinedMetadataFile?> ProcessAsync(
        MetadataFile rawMetadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs all registered groomers over the provided raw metadata file, saves the refined
    /// metadata to disk next to the media file, and returns the output file path.
    /// </summary>
    /// <param name="rawMetadata">The raw metadata produced by Layer 1 scanners.</param>
    /// <param name="rawMetadataFilePath">
    /// Path to the serialized raw metadata file on disk, stored in the refined output for traceability.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The path of the saved refined metadata file, or null if no groomers are registered.
    /// </returns>
    Task<string?> ProcessAndSaveAsync(
        MetadataFile rawMetadata,
        string rawMetadataFilePath,
        CancellationToken cancellationToken = default);
}
