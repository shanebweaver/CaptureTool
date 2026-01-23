namespace CaptureTool.Domain.Capture.Interfaces.Metadata;

/// <summary>
/// Base interface for metadata scanners that analyze audio or video data.
/// </summary>
public interface IMetadataScanner
{
    /// <summary>
    /// Gets the unique identifier for this scanner type.
    /// </summary>
    string ScannerId { get; }

    /// <summary>
    /// Gets a human-readable name for this scanner.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the type of data this scanner processes.
    /// </summary>
    MetadataScannerType ScannerType { get; }
}

/// <summary>
/// Interface for video frame metadata scanners.
/// </summary>
public interface IVideoMetadataScanner : IMetadataScanner
{
    /// <summary>
    /// Scans a video frame and returns metadata.
    /// </summary>
    /// <param name="frameData">The video frame data to scan.</param>
    /// <returns>Metadata extracted from the frame, or null if no metadata found.</returns>
    Task<MetadataEntry?> ScanFrameAsync(VideoFrameData frameData, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for audio sample metadata scanners.
/// </summary>
public interface IAudioMetadataScanner : IMetadataScanner
{
    /// <summary>
    /// Scans an audio sample and returns metadata.
    /// </summary>
    /// <param name="sampleData">The audio sample data to scan.</param>
    /// <returns>Metadata extracted from the sample, or null if no metadata found.</returns>
    Task<MetadataEntry?> ScanSampleAsync(AudioSampleData sampleData, CancellationToken cancellationToken = default);
}
