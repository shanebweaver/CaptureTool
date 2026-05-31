using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.Infrastructure.Abstractions.Logging;
using CaptureTool.Infrastructure.Abstractions.Storage;
using System.Collections.Concurrent;
using System.Text.Json;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;

namespace CaptureTool.Domain.Capture.Windows.Metadata;

/// <summary>
/// Service for managing metadata scanning jobs with persistent queue.
/// Jobs are saved to disk and can be resumed after app restart.
/// </summary>
public sealed class MetadataScanningService : IMetadataScanningService, IDisposable
{
    private readonly IMetadataScannerRegistry _registry;
    private readonly ILogService _logService;
    private readonly PersistentJobQueueManager _queueManager;
    private readonly ConcurrentDictionary<Guid, MetadataScanJob> _activeJobs = new();
    private readonly BlockingCollection<MetadataScanJob> _jobQueue = new();
    private readonly CancellationTokenSource _serviceCancellation = new();
    private readonly Task _processingTask;

    public MetadataScanningService(
        IMetadataScannerRegistry registry,
        IStorageService storageService,
        ILogService logService)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));

        _queueManager = new PersistentJobQueueManager(storageService, logService);

        // Load any pending jobs from previous session (async without blocking constructor)
        _ = Task.Run(async () =>
        {
            try
            {
                await LoadPendingJobsAsync();
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, $"Error loading pending jobs during startup: {ex.Message}");
            }
        });

        // Start background processing
        _processingTask = Task.Run(ProcessJobsAsync);
    }

    private async Task LoadPendingJobsAsync()
    {
        try
        {
            var pendingRequests = await _queueManager.LoadPendingJobsAsync();

            _logService.LogInformation($"Loading {pendingRequests.Count} pending job(s) from previous session");

            foreach (var request in pendingRequests)
            {
                // Verify the media file still exists
                if (!File.Exists(request.MediaFilePath))
                {
                    _logService.LogWarning($"Media file not found for job {request.JobId}, skipping: {request.MediaFilePath}");
                    _queueManager.DeleteJobRequest(request.JobId);
                    continue;
                }

                var job = new MetadataScanJob(request.JobId, request.MediaFilePath);
                _activeJobs[job.JobId] = job;
                _jobQueue.Add(job);

                _logService.LogInformation($"Restored job {request.JobId} for file: {request.MediaFilePath}");
            }
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, $"Error loading pending jobs: {ex.Message}");
        }
    }

    public IMetadataScanJob QueueScan(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        var jobId = Guid.NewGuid();
        var request = new ScanJobRequest(jobId, filePath);

        // Persist the request before queueing so a fast job cannot complete
        // while its queue file is still being written.
        _queueManager.SaveJobRequest(request);

        // Create and queue the job immediately
        var job = new MetadataScanJob(jobId, filePath);
        _activeJobs[job.JobId] = job;
        _jobQueue.Add(job);

        _logService.LogInformation($"Queued metadata scan job {job.JobId} for file: {filePath}");

        return job;
    }

    public IReadOnlyList<IMetadataScanJob> GetActiveJobs()
    {
        return _activeJobs.Values
            .Where(j => j.Status == MetadataScanJobStatus.Queued ||
                       j.Status == MetadataScanJobStatus.Processing)
            .Cast<IMetadataScanJob>()
            .ToList();
    }

    public IMetadataScanJob? GetJob(Guid jobId)
    {
        return _activeJobs.TryGetValue(jobId, out var job) ? job : null;
    }

    public void CancelAllJobs()
    {
        foreach (var job in _activeJobs.Values)
        {
            job.Cancel();
        }
    }

    private async Task ProcessJobsAsync()
    {
        try
        {
            while (!_serviceCancellation.Token.IsCancellationRequested)
            {
                MetadataScanJob? job = null;
                try
                {
                    // Wait for a job with cancellation support
                    job = _jobQueue.Take(_serviceCancellation.Token);
                    await ProcessJobAsync(job);
                }
                catch (OperationCanceledException) when (_serviceCancellation.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (job != null)
                    {
                        job.SetError($"Unexpected error: {ex.Message}");
                    }
                    _logService.LogException(ex, $"Error processing metadata scan job: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, $"Fatal error in metadata scanning service: {ex.Message}");
        }
    }

    private async Task ProcessJobAsync(MetadataScanJob job)
    {
        try
        {
            job.UpdateStatus(MetadataScanJobStatus.Processing);
            _logService.LogInformation($"Processing metadata scan job {job.JobId}");

            var metadataEntries = new List<MetadataEntry>();
            var scannerInfo = new Dictionary<string, string>();

            var videoScanners = _registry.GetVideoScanners();
            var audioScanners = _registry.GetAudioScanners();

            foreach (var scanner in videoScanners)
            {
                scannerInfo[scanner.ScannerId] = scanner.Name;
            }

            foreach (var scanner in audioScanners)
            {
                scannerInfo[scanner.ScannerId] = scanner.Name;
            }

            // Update progress during processing
            job.UpdateProgress(5);

            await ScanVideoFileFramesAsync(job, videoScanners, metadataEntries);

            // Create metadata file
            var metadataFile = new MetadataFile(
                job.FilePath,
                DateTime.UtcNow,
                metadataEntries,
                scannerInfo
            );

            // Save metadata file next to the media file
            string metadataPath = Path.ChangeExtension(job.FilePath, MetadataFile.FileExtension);
            await SaveMetadataFileAsync(metadataFile, metadataPath, job.CancellationToken);

            job.Complete(metadataPath);
            _logService.LogInformation($"Completed metadata scan job {job.JobId}. Output: {metadataPath}");

            // Delete the job request file from queue now that it's complete
            _queueManager.DeleteJobRequest(job.JobId);

            // Clean up completed job from active jobs after a delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(5));
                if (_activeJobs.TryRemove(job.JobId, out var removedJob))
                {
                    removedJob.Dispose();
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Job was cancelled
            _logService.LogInformation($"Metadata scan job {job.JobId} was cancelled");

            // Delete the job request file when cancelled
            _queueManager.DeleteJobRequest(job.JobId);
        }
        catch (Exception ex)
        {
            job.SetError(ex.Message);
            _logService.LogException(ex, $"Failed to process metadata scan job {job.JobId}: {ex.Message}");

            // Keep the job request file so it can be retried on next startup
            // Optionally, delete it after multiple failures
        }
    }

    private async Task ScanVideoFileFramesAsync(
        MetadataScanJob job,
        IReadOnlyList<IVideoMetadataScanner> videoScanners,
        List<MetadataEntry> metadataEntries)
    {
        var bitmapScanners = videoScanners
            .OfType<ISoftwareBitmapVideoMetadataScanner>()
            .ToList();

        if (bitmapScanners.Count == 0)
        {
            _logService.LogInformation("No software bitmap video metadata scanners are registered for file scanning.");
            job.UpdateProgress(50);
            return;
        }

        StorageFile file = await StorageFile.GetFileFromPathAsync(job.FilePath);
        MediaClip clip = await MediaClip.CreateFromFileAsync(file);
        var composition = new MediaComposition();
        composition.Clips.Add(clip);

        TimeSpan duration = clip.OriginalDuration;
        if (duration <= TimeSpan.Zero)
        {
            _logService.LogWarning($"Could not determine duration for metadata scan file: {job.FilePath}");
            job.UpdateProgress(50);
            return;
        }

        const int sampleIntervalSeconds = 1;
        int sampleCount = Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds / sampleIntervalSeconds));
        _logService.LogInformation($"Scanning {sampleCount} video frame sample(s) from {Path.GetFileName(job.FilePath)}");

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            job.CancellationToken.ThrowIfCancellationRequested();

            TimeSpan timestamp = TimeSpan.FromSeconds(sampleIndex * sampleIntervalSeconds);
            if (timestamp >= duration)
            {
                timestamp = duration - TimeSpan.FromMilliseconds(1);
            }

            using var frameStream = await composition.GetThumbnailAsync(
                timestamp,
                0,
                0,
                VideoFramePrecision.NearestFrame);

            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(frameStream);
            using SoftwareBitmap frameBitmap = await decoder.GetSoftwareBitmapAsync();

            foreach (ISoftwareBitmapVideoMetadataScanner scanner in bitmapScanners)
            {
                MetadataEntry? entry = await scanner.ScanBitmapAsync(
                    frameBitmap,
                    timestamp.Ticks,
                    job.CancellationToken);

                if (entry is not null)
                {
                    metadataEntries.Add(entry);
                }
            }

            double progress = 5 + (sampleIndex + 1) * 90d / sampleCount;
            job.UpdateProgress(progress);
        }
    }

    private async Task SaveMetadataFileAsync(MetadataFile metadataFile, string path, CancellationToken cancellationToken)
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
        await JsonSerializer.SerializeAsync(stream, dto, MetadataJsonContext.Default.MetadataFileDto, cancellationToken);
    }

    public void Dispose()
    {
        _serviceCancellation.Cancel();
        _jobQueue.CompleteAdding();

        try
        {
            // Give jobs more time to complete gracefully since scans can take a while
            if (!_processingTask.Wait(TimeSpan.FromSeconds(30)))
            {
                _logService.LogWarning("Metadata scanning service background task did not complete within 30 seconds");
            }
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, $"Error waiting for metadata scanning service to complete: {ex.Message}");
        }

        // Dispose all remaining active jobs
        foreach (var job in _activeJobs.Values)
        {
            job.Dispose();
        }
        _activeJobs.Clear();

        _serviceCancellation.Dispose();
        _jobQueue.Dispose();
    }
}
