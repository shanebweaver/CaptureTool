namespace CaptureTool.MetadataScanner.Windows.WinUI.Metadata;

public enum MetadataScannerType
{
    MediaFile
}

public interface IMetadataScanner
{
    string ScannerId { get; }
    string Name { get; }
    MetadataScannerType ScannerType { get; }
}

public interface IMediaFileMetadataScanner : IMetadataScanner
{
    Task<IReadOnlyList<MetadataEntry>> ScanFileAsync(string filePath, CancellationToken cancellationToken = default);
}

public interface IMetadataScannerRegistry
{
    void RegisterMediaFileScanner(IMediaFileMetadataScanner scanner);
    bool Unregister(string scannerId);
    IReadOnlyList<IMediaFileMetadataScanner> GetMediaFileScanners();
    IReadOnlyList<IMetadataScanner> GetAllScanners();
}

public sealed class MetadataScannerRegistry : IMetadataScannerRegistry
{
    private readonly Dictionary<string, IMediaFileMetadataScanner> mediaFileScanners = new();
    private readonly object syncRoot = new();

    public void RegisterMediaFileScanner(IMediaFileMetadataScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(scanner);

        lock (syncRoot)
        {
            if (mediaFileScanners.ContainsKey(scanner.ScannerId))
            {
                throw new InvalidOperationException(
                    $"A media file scanner with ID '{scanner.ScannerId}' is already registered.");
            }

            mediaFileScanners[scanner.ScannerId] = scanner;
        }
    }

    public bool Unregister(string scannerId)
    {
        ArgumentNullException.ThrowIfNull(scannerId);

        lock (syncRoot)
        {
            return mediaFileScanners.Remove(scannerId);
        }
    }

    public IReadOnlyList<IMediaFileMetadataScanner> GetMediaFileScanners()
    {
        lock (syncRoot)
        {
            return mediaFileScanners.Values.ToList();
        }
    }

    public IReadOnlyList<IMetadataScanner> GetAllScanners()
    {
        lock (syncRoot)
        {
            return mediaFileScanners.Values.Cast<IMetadataScanner>().ToList();
        }
    }
}
