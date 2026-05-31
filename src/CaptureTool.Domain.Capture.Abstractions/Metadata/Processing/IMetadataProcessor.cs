namespace CaptureTool.Domain.Capture.Abstractions.Metadata.Processing;

/// <summary>
/// Refines raw metadata entries into higher-level insights.
/// </summary>
public interface IMetadataProcessor
{
    string ProcessorId { get; }

    string Name { get; }

    /// <summary>
    /// Gets the raw metadata keys this processor consumes. Empty means all entries.
    /// </summary>
    IReadOnlyList<string> SupportedKeys { get; }

    Task<IReadOnlyList<InsightEntry>> ProcessAsync(
        IReadOnlyList<MetadataEntry> rawEntries,
        CancellationToken cancellationToken = default);
}
