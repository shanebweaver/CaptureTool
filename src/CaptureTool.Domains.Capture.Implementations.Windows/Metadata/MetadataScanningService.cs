using System.Collections.Concurrent;
using System.Text.Json;
using CaptureTool.Domains.Capture.Interfaces.Metadata;
using CaptureTool.Services.Interfaces.Logging;

namespace CaptureTool.Domains.Capture.Implementations.Windows.Metadata;

/// <summary>
/// Service for managing metadata scanning jobs with async queue processing.
/// </summary>
public sealed class MetadataScanningService : IMetadataScanningService, IDisposable
{
    private readonly IMetadataScannerRegistry _registry;
    private readonly ILogService _logService;
    private readonly ConcurrentDictionary<Guid, MetadataScanJob> _jobs = new();
    private readonly BlockingCollection<MetadataScanJob> _jobQueue = new();
    private readonly CancellationTokenSource _serviceCancellation = new();
    private readonly Task _processingTask;
    private const int MaxCompletedJobsRetained = 100;

    public MetadataScanningService(
        IMetadataScannerRegistry registry,
        ILogService logService)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));

        // Start background processing
        _processingTask = Task.Run(ProcessJobsAsync);
    }

    public IMetadataScanJob QueueScan(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }

        var job = new MetadataScanJob(Guid.NewGuid(), filePath);
        _jobs[job.JobId] = job;
        _jobQueue.Add(job);

        _logService.LogInformation($"Queued metadata scan job {job.JobId} for file: {filePath}");

        return job;
    }

    public IReadOnlyList<IMetadataScanJob> GetActiveJobs()
    {
        return _jobs.Values
            .Where(j => j.Status == MetadataScanJobStatus.Queued || 
                       j.Status == MetadataScanJobStatus.Processing)
            .Cast<IMetadataScanJob>()
            .ToList();
    }

    public IMetadataScanJob? GetJob(Guid jobId)
    {
        return _jobs.TryGetValue(jobId, out var job) ? job : null;
    }

    public void CancelAllJobs()
    {
        foreach (var job in _jobs.Values)
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

            // Save to JSON file
            string metadataPath = Path.ChangeExtension(job.FilePath, ".metadata.json");
            await SaveMetadataFileAsync(metadataFile, metadataPath, job.CancellationToken);

            job.Complete(metadataPath);
            _logService.LogInformation($"Completed metadata scan job {job.JobId}. Output: {metadataPath}");
            
            // Clean up old completed jobs to prevent memory leak
            CleanupOldJobs();
        }
        catch (OperationCanceledException)
        {
            // Job was already cancelled, no need to call Cancel() again
            _logService.LogInformation($"Metadata scan job {job.JobId} was cancelled");
        }
        catch (Exception ex)
        {
            job.SetError(ex.Message);
            _logService.LogError($"Failed to process metadata scan job {job.JobId}: {ex.Message}", ex);
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

    private void CleanupOldJobs()
    {
        // Remove old completed/failed/cancelled jobs if we exceed the retention limit
        var completedJobs = _jobs.Values
            .Where(j => j.Status == MetadataScanJobStatus.Completed ||
                       j.Status == MetadataScanJobStatus.Failed ||
                       j.Status == MetadataScanJobStatus.Cancelled)
            .OrderBy(j => j.Status == MetadataScanJobStatus.Completed ? 1 : 0) // Keep completed jobs longer
            .ToList();

        if (completedJobs.Count > MaxCompletedJobsRetained)
        {
            var jobsToRemove = completedJobs.Take(completedJobs.Count - MaxCompletedJobsRetained);
            foreach (var job in jobsToRemove)
            {
                if (_jobs.TryRemove(job.JobId, out var removedJob))
                {
                    removedJob.Dispose();
                }
            }
        }
    }

    public void Dispose()
    {
        _serviceCancellation.Cancel();
        _jobQueue.CompleteAdding();

        try
        {
            if (!_processingTask.Wait(TimeSpan.FromSeconds(5)))
            {
                _logService.LogWarning("Metadata scanning service background task did not complete within timeout");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error waiting for metadata scanning service to complete: {ex.Message}", ex);
        }

        // Dispose all remaining jobs
        foreach (var job in _jobs.Values)
        {
            job.Dispose();
        }
        _jobs.Clear();

        _serviceCancellation.Dispose();
        _jobQueue.Dispose();
    }
}
