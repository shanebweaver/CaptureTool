namespace CaptureTool.Domain.Capture.Interfaces.Metadata.Grooming;

/// <summary>
/// Interface for a metadata groomer that refines raw metadata entries into high-level insights.
/// Groomers form the second processing layer: they consume raw scan output and produce
/// intentional, timestamped data that can power an enhanced playback experience.
/// </summary>
public interface IMetadataGroomer
{
    /// <summary>
    /// Gets the unique identifier for this groomer.
    /// </summary>
    string GroomerId { get; }

    /// <summary>
    /// Gets a human-readable name for this groomer.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the raw metadata entry keys this groomer is interested in.
    /// An empty collection means the groomer will receive all entries.
    /// </summary>
    IReadOnlyList<string> SupportedKeys { get; }

    /// <summary>
    /// Processes a collection of raw metadata entries and produces refined insights.
    /// </summary>
    /// <param name="rawEntries">The raw metadata entries from Layer 1 scanners.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of refined insights, ordered by timestamp.</returns>
    Task<IReadOnlyList<InsightEntry>> GroomAsync(
        IReadOnlyList<MetadataEntry> rawEntries,
        CancellationToken cancellationToken = default);
}
