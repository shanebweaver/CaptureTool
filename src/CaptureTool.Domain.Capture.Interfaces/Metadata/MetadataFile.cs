namespace CaptureTool.Domain.Capture.Interfaces.Metadata;

/// <summary>
/// Represents a metadata file containing scan results.
/// </summary>
public sealed class MetadataFile
{
    /// <summary>
    /// Gets the source file path that was scanned.
    /// </summary>
    public string SourceFilePath { get; }

    /// <summary>
    /// Gets the timestamp when the scan was performed.
    /// </summary>
    public DateTime ScanTimestamp { get; }

    /// <summary>
    /// Gets the list of metadata entries.
    /// </summary>
    public IReadOnlyList<MetadataEntry> Entries { get; }

    /// <summary>
    /// Gets metadata about the scanners used.
    /// </summary>
    public IReadOnlyDictionary<string, string> ScannerInfo { get; }

    public MetadataFile(
        string sourceFilePath,
        DateTime scanTimestamp,
        IReadOnlyList<MetadataEntry> entries,
        IReadOnlyDictionary<string, string> scannerInfo)
    {
        SourceFilePath = sourceFilePath ?? throw new ArgumentNullException(nameof(sourceFilePath));
        ScanTimestamp = scanTimestamp;
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        ScannerInfo = scannerInfo ?? throw new ArgumentNullException(nameof(scannerInfo));
    }
}
