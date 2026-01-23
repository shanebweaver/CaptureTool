using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Domain.Capture.Interfaces.Metadata;

namespace CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Scanners;

/// <summary>
/// Example video metadata scanner that extracts basic frame information.
/// </summary>
public sealed class BasicVideoFrameScanner : IVideoMetadataScanner
{
    public string ScannerId => "basic-video-frame";
    public string Name => "Basic Video Frame Scanner";
    public MetadataScannerType ScannerType => MetadataScannerType.Video;

    public Task<MetadataEntry?> ScanFrameAsync(VideoFrameData frameData, CancellationToken cancellationToken = default)
    {
        // Example: Extract basic frame information
        var metadata = new MetadataEntry(
            timestamp: frameData.Timestamp,
            scannerId: ScannerId,
            key: "frame-info",
            value: $"{frameData.Width}x{frameData.Height}",
            additionalData: new Dictionary<string, object?>
            {
                ["width"] = frameData.Width,
                ["height"] = frameData.Height,
                ["hasTexture"] = frameData.pTexture != IntPtr.Zero
            }
        );

        return Task.FromResult<MetadataEntry?>(metadata);
    }
}
