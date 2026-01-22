namespace CaptureTool.Domain.Capture.Interfaces.Metadata;

/// <summary>
/// Registry for managing metadata scanners.
/// </summary>
public interface IMetadataScannerRegistry
{
    /// <summary>
    /// Registers a video metadata scanner.
    /// </summary>
    /// <param name="scanner">The scanner to register.</param>
    void RegisterVideoScanner(IVideoMetadataScanner scanner);

    /// <summary>
    /// Registers an audio metadata scanner.
    /// </summary>
    /// <param name="scanner">The scanner to register.</param>
    void RegisterAudioScanner(IAudioMetadataScanner scanner);

    /// <summary>
    /// Unregisters a scanner by its ID.
    /// </summary>
    /// <param name="scannerId">The ID of the scanner to unregister.</param>
    /// <returns>True if the scanner was unregistered, false if not found.</returns>
    bool Unregister(string scannerId);

    /// <summary>
    /// Gets all registered video scanners.
    /// </summary>
    IReadOnlyList<IVideoMetadataScanner> GetVideoScanners();

    /// <summary>
    /// Gets all registered audio scanners.
    /// </summary>
    IReadOnlyList<IAudioMetadataScanner> GetAudioScanners();

    /// <summary>
    /// Gets all registered scanners.
    /// </summary>
    IReadOnlyList<IMetadataScanner> GetAllScanners();
}
