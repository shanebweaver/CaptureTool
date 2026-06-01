namespace CaptureTool.MetadataScanner.Windows.WinUI.Metadata;

public sealed class MetadataFile(
    string sourceFilePath,
    DateTime scanTimestamp,
    IReadOnlyList<MetadataEntry> entries,
    IReadOnlyDictionary<string, string> scannerInfo)
{
    public const string FileExtension = ".metadata.json";

    public string SourceFilePath { get; } = sourceFilePath ?? throw new ArgumentNullException(nameof(sourceFilePath));
    public DateTime ScanTimestamp { get; } = scanTimestamp;
    public IReadOnlyList<MetadataEntry> Entries { get; } = entries ?? throw new ArgumentNullException(nameof(entries));
    public IReadOnlyDictionary<string, string> ScannerInfo { get; } = scannerInfo ?? throw new ArgumentNullException(nameof(scannerInfo));
}
