namespace CaptureTool.Domain.Capture.Interfaces.Metadata.Processing;

/// <summary>
/// Interface for a metadata processor that refines raw metadata entries into high-level insights.
/// Processors form the second processing layer: they consume raw scan output and produce
/// intentional, timestamped data that can power an enhanced playback experience.
/// </summary>
public interface IMetadataProcessor
{
    /// <summary>
    /// Gets the unique identifier for this processor.
    /// </summary>
    string ProcessorId { get; }

    /// <summary>
    /// Gets a human-readable name for this processor.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the raw metadata entry keys this processor is interested in.
    /// An empty collection means the processor will receive all entries.
    /// </summary>
    IReadOnlyList<string> SupportedKeys { get; }

    /// <summary>
    /// Processes a collection of raw metadata entries and produces refined insights.
    /// </summary>
    /// <param name="rawEntries">The raw metadata entries from Layer 1 scanners.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of refined insights, ordered by timestamp.</returns>
    Task<IReadOnlyList<InsightEntry>> ProcessAsync(
        IReadOnlyList<MetadataEntry> rawEntries,
        CancellationToken cancellationToken = default);
}
