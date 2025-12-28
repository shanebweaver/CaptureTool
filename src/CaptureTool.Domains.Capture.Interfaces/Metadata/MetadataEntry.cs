namespace CaptureTool.Domains.Capture.Interfaces.Metadata;

/// <summary>
/// Represents a single metadata entry extracted from audio or video data.
/// </summary>
public sealed class MetadataEntry
{
    /// <summary>
    /// Gets the timestamp of this metadata entry in 100ns ticks.
    /// </summary>
    public long Timestamp { get; }

    /// <summary>
    /// Gets the scanner ID that produced this metadata.
    /// </summary>
    public string ScannerId { get; }

    /// <summary>
    /// Gets the metadata key/type identifier.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the metadata value.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Gets additional contextual data for this metadata entry.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? AdditionalData { get; }

    public MetadataEntry(
        long timestamp,
        string scannerId,
        string key,
        object? value,
        IReadOnlyDictionary<string, object?>? additionalData = null)
    {
        Timestamp = timestamp;
        ScannerId = scannerId ?? throw new ArgumentNullException(nameof(scannerId));
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Value = value;
        AdditionalData = additionalData;
    }
}
