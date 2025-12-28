using CaptureTool.Domains.Capture.Interfaces.Metadata;

namespace CaptureTool.Domains.Capture.Implementations.Windows.Metadata;

/// <summary>
/// Implementation of the metadata scanner registry.
/// </summary>
public sealed class MetadataScannerRegistry : IMetadataScannerRegistry
{
    private readonly Dictionary<string, IVideoMetadataScanner> _videoScanners = new();
    private readonly Dictionary<string, IAudioMetadataScanner> _audioScanners = new();
    private readonly object _lock = new();

    public void RegisterVideoScanner(IVideoMetadataScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(scanner);

        lock (_lock)
        {
            _videoScanners[scanner.ScannerId] = scanner;
        }
    }

    public void RegisterAudioScanner(IAudioMetadataScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(scanner);

        lock (_lock)
        {
            _audioScanners[scanner.ScannerId] = scanner;
        }
    }

    public bool Unregister(string scannerId)
    {
        ArgumentNullException.ThrowIfNull(scannerId);

        lock (_lock)
        {
            return _videoScanners.Remove(scannerId) || _audioScanners.Remove(scannerId);
        }
    }

    public IReadOnlyList<IVideoMetadataScanner> GetVideoScanners()
    {
        lock (_lock)
        {
            return _videoScanners.Values.ToList();
        }
    }

    public IReadOnlyList<IAudioMetadataScanner> GetAudioScanners()
    {
        lock (_lock)
        {
            return _audioScanners.Values.ToList();
        }
    }

    public IReadOnlyList<IMetadataScanner> GetAllScanners()
    {
        lock (_lock)
        {
            var all = new List<IMetadataScanner>();
            all.AddRange(_videoScanners.Values);
            all.AddRange(_audioScanners.Values);
            return all;
        }
    }
}
