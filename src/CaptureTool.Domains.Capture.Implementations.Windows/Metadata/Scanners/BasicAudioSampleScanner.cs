using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Domains.Capture.Interfaces.Metadata;

namespace CaptureTool.Domains.Capture.Implementations.Windows.Metadata.Scanners;

/// <summary>
/// Example audio metadata scanner that extracts basic audio sample information.
/// </summary>
public sealed class BasicAudioSampleScanner : IAudioMetadataScanner
{
    public string ScannerId => "basic-audio-sample";
    public string Name => "Basic Audio Sample Scanner";
    public MetadataScannerType ScannerType => MetadataScannerType.Audio;

    public Task<MetadataEntry?> ScanSampleAsync(AudioSampleData sampleData, CancellationToken cancellationToken = default)
    {
        // Example: Extract basic audio information
        var metadata = new MetadataEntry(
            timestamp: sampleData.Timestamp,
            scannerId: ScannerId,
            key: "audio-info",
            value: $"{sampleData.SampleRate}Hz {sampleData.Channels}ch",
            additionalData: new Dictionary<string, object?>
            {
                ["sampleRate"] = sampleData.SampleRate,
                ["channels"] = sampleData.Channels,
                ["bitsPerSample"] = sampleData.BitsPerSample,
                ["numFrames"] = sampleData.NumFrames
            }
        );

        return Task.FromResult<MetadataEntry?>(metadata);
    }
}
