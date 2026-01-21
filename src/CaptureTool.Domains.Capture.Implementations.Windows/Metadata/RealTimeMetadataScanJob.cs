using System.Collections.Concurrent;
using System.Text.Json;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Domains.Capture.Interfaces.Metadata;
using CaptureTool.Core.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Logging;

namespace CaptureTool.Domains.Capture.Implementations.Windows.Metadata;

/// <summary>
/// Metadata scan job that processes video frames and audio samples in real-time during recording.
/// Collects metadata as the recording happens, then saves to disk when finalized.
/// </summary>
public sealed class RealTimeMetadataScanJob : IRealTimeMetadataScanJob
{
    private readonly Guid _jobId;
    private readonly string _filePath;
    private readonly IMetadataScannerRegistry _registry;
    private readonly ILogService _logService;
    private readonly ConcurrentBag<MetadataEntry> _entries;
    private readonly Dictionary<string, string> _scannerInfo;
    private MetadataScanJobStatus _status;
    private string? _metadataFilePath;
    private string? _errorMessage;
    private int _videoFrameCount;
    private int _audioSampleCount;

    public RealTimeMetadataScanJob(
        Guid jobId,
        string filePath,
        IMetadataScannerRegistry registry,
        ILogService logService)
    {
        _jobId = jobId;
        _filePath = filePath;
        _registry = registry;
        _logService = logService;
        _entries = new ConcurrentBag<MetadataEntry>();
        _scannerInfo = new Dictionary<string, string>();
        _status = MetadataScanJobStatus.Processing;

        // Collect scanner info
        var videoScanners = _registry.GetVideoScanners();
        var audioScanners = _registry.GetAudioScanners();

        foreach (var scanner in videoScanners)
        {
            _scannerInfo[scanner.ScannerId] = scanner.Name;
        }

        foreach (var scanner in audioScanners)
        {
            _scannerInfo[scanner.ScannerId] = scanner.Name;
        }

        _logService.LogInformation($"Started real-time metadata collection for {Path.GetFileName(filePath)}");
    }

    public Guid JobId => _jobId;
    public string FilePath => _filePath;
    public MetadataScanJobStatus Status => _status;
    public double Progress => 100.0; // Always "complete" since we're doing real-time
    public string? ErrorMessage => _errorMessage;
    public string? MetadataFilePath => _metadataFilePath;

    public event EventHandler<MetadataScanJobStatus>? StatusChanged;

#pragma warning disable CS0067 // Event is never used (required by interface)
    public event EventHandler<double>? ProgressChanged;
#pragma warning restore CS0067

    /// <summary>
    /// Processes a video frame through all registered video scanners.
    /// </summary>
    public void ProcessVideoFrame(ref VideoFrameData frameData)
    {
        _videoFrameCount++;
        
        var videoScanners = _registry.GetVideoScanners();
        foreach (var scanner in videoScanners)
        {
            try
            {
                // Call async method synchronously (blocking) since we're in callback context
                var entry = scanner.ScanFrameAsync(frameData, CancellationToken.None).GetAwaiter().GetResult();
                if (entry != null)
                {
                    _entries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Video scanner '{scanner.ScannerId}' failed: {ex.Message}");
            }
        }

        // Log periodically
        if (_videoFrameCount % 100 == 0)
        {
            _logService.LogInformation($"Processed {_videoFrameCount} video frames, collected {_entries.Count} metadata entries");
        }
    }

    /// <summary>
    /// Processes an audio sample through all registered audio scanners.
    /// </summary>
    public void ProcessAudioSample(ref AudioSampleData sampleData)
    {
        _audioSampleCount++;
        
        var audioScanners = _registry.GetAudioScanners();
        foreach (var scanner in audioScanners)
        {
            try
            {
                // Call async method synchronously (blocking) since we're in callback context
                var entry = scanner.ScanSampleAsync(sampleData, CancellationToken.None).GetAwaiter().GetResult();
                if (entry != null)
                {
                    _entries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Audio scanner '{scanner.ScannerId}' failed: {ex.Message}");
            }
        }

        // Log periodically
        if (_audioSampleCount % 1000 == 0)
        {
            _logService.LogInformation($"Processed {_audioSampleCount} audio samples, collected {_entries.Count} metadata entries");
        }
    }

    /// <summary>
    /// Finalizes the metadata collection and saves to disk.
    /// </summary>
    public async Task FinalizeAndSaveAsync()
    {
        try
        {
            _logService.LogInformation($"Finalizing metadata: {_videoFrameCount} video frames, {_audioSampleCount} audio samples, {_entries.Count} entries");

            // Create metadata file
            var metadataFile = new MetadataFile(
                _filePath,
                DateTime.UtcNow,
                _entries.ToList(),
                _scannerInfo
            );

            // Save metadata file next to the media file
            _metadataFilePath = Path.ChangeExtension(_filePath, ".metadata.json");
            await SaveMetadataFileAsync(metadataFile, _metadataFilePath);

            _status = MetadataScanJobStatus.Completed;
            StatusChanged?.Invoke(this, _status);
            _logService.LogInformation($"Saved metadata to: {_metadataFilePath}");
        }
        catch (Exception ex)
        {
            _status = MetadataScanJobStatus.Failed;
            _errorMessage = ex.Message;
            StatusChanged?.Invoke(this, _status);
            _logService.LogException(ex, $"Failed to save metadata: {ex.Message}");
            throw;
        }
    }

    private async Task SaveMetadataFileAsync(MetadataFile metadataFile, string path)
    {
        // Convert to DTO for serialization
        var dto = new MetadataFileDto
        {
            SourceFilePath = metadataFile.SourceFilePath,
            ScanTimestamp = metadataFile.ScanTimestamp,
            ScannerInfo = new Dictionary<string, string>(metadataFile.ScannerInfo),
            Entries = metadataFile.Entries.Select(e => new MetadataEntryDto
            {
                Timestamp = e.Timestamp,
                ScannerId = e.ScannerId,
                Key = e.Key,
                Value = e.Value?.ToString(),
                AdditionalData = e.AdditionalData?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.ToString() ?? string.Empty)
            }).ToList()
        };

        using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, dto, MetadataJsonContext.Default.MetadataFileDto);
    }

    public void Cancel()
    {
        _status = MetadataScanJobStatus.Cancelled;
        StatusChanged?.Invoke(this, _status);
    }
}
