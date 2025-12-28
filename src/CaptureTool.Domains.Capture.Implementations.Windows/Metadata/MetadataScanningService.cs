using System.Collections.Concurrent;
using System.Text.Json;
using CaptureTool.Domains.Capture.Interfaces.Metadata;
using CaptureTool.Services.Interfaces.Logging;
using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Domains.Capture.Implementations.Windows.Metadata;

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

        // Load any pending jobs from previous session
        LoadPendingJobs();

        // Start background processing
        _processingTask = Task.Run(ProcessJobsAsync);
    }

    private void LoadPendingJobs()
    {
        try
        {
            var pendingRequests = _queueManager.LoadPendingJobsAsync().GetAwaiter().GetResult();
            
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
            _logService.LogError($"Error loading pending jobs: {ex.Message}", ex);
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

        // Save job request to disk first
        try
        {
            _queueManager.SaveJobRequestAsync(request).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Failed to save job request for {filePath}: {ex.Message}", ex);
            throw new InvalidOperationException("Failed to queue scan job", ex);
        }

        // Create and queue the job
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
                    _logService.LogError($"Error processing metadata scan job: {ex.Message}", ex);
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Fatal error in metadata scanning service: {ex.Message}", ex);
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

            // For now, this is a placeholder that would process the actual file
            // In a real implementation, you would:
            // 1. Open the video file
            // 2. Iterate through frames/samples
            // 3. Call registered scanners
            // 4. Collect metadata entries

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
            job.UpdateProgress(50);

            // Create metadata file
            var metadataFile = new MetadataFile(
                job.FilePath,
                DateTime.UtcNow,
                metadataEntries,
                scannerInfo
            );

            // Save metadata file next to the media file
            string metadataPath = Path.ChangeExtension(job.FilePath, ".metadata.json");
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
            _logService.LogError($"Failed to process metadata scan job {job.JobId}: {ex.Message}", ex);
            
            // Keep the job request file so it can be retried on next startup
            // Optionally, delete it after multiple failures
        }
    }

    private async Task SaveMetadataFileAsync(MetadataFile metadataFile, string path, CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Create a serializable representation
        var data = new
        {
            sourceFilePath = metadataFile.SourceFilePath,
            scanTimestamp = metadataFile.ScanTimestamp,
            scannerInfo = metadataFile.ScannerInfo,
            entries = metadataFile.Entries.Select(e => new
            {
                timestamp = e.Timestamp,
                scannerId = e.ScannerId,
                key = e.Key,
                value = e.Value,
                additionalData = e.AdditionalData
            })
        };

        using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, data, options, cancellationToken);
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
            _logService.LogError($"Error waiting for metadata scanning service to complete: {ex.Message}", ex);
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
