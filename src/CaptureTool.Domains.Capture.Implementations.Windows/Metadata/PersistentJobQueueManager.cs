using System.Text.Json;
using CaptureTool.Domains.Capture.Interfaces.Metadata;
using CaptureTool.Services.Interfaces.Logging;
using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Domains.Capture.Implementations.Windows.Metadata;

/// <summary>
/// Manages persistent job queue using JSON files in a temp folder.
/// Job requests are saved as files and deleted when complete.
/// </summary>
internal sealed class PersistentJobQueueManager
{
    private readonly string _queueFolderPath;
    private readonly ILogService _logService;

    public PersistentJobQueueManager(IStorageService storageService, ILogService logService)
    {
        ArgumentNullException.ThrowIfNull(storageService);
        ArgumentNullException.ThrowIfNull(logService);

        _logService = logService;
        _queueFolderPath = Path.Combine(
            storageService.GetApplicationTemporaryFolderPath(),
            "ScanJobQueue"
        );

        EnsureQueueFolderExists();
    }

    private void EnsureQueueFolderExists()
    {
        if (!Directory.Exists(_queueFolderPath))
        {
            Directory.CreateDirectory(_queueFolderPath);
            _logService.LogInformation($"Created scan job queue folder: {_queueFolderPath}");
        }
    }

    /// <summary>
    /// Saves a job request to the queue folder.
    /// </summary>
    public async Task SaveJobRequestAsync(ScanJobRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string fileName = GetJobFileName(request.JobId);
        string filePath = Path.Combine(_queueFolderPath, fileName);

        try
        {
            using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, request, MetadataJsonContext.Default.ScanJobRequest, cancellationToken);
            _logService.LogInformation($"Saved job request {request.JobId} to queue: {filePath}");
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, $"Failed to save job request {request.JobId}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Loads all pending job requests from the queue folder.
    /// </summary>
    public async Task<List<ScanJobRequest>> LoadPendingJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = new List<ScanJobRequest>();

        try
        {
            EnsureQueueFolderExists();

            var queueFiles = Directory.GetFiles(_queueFolderPath, "*.json");
            _logService.LogInformation($"Found {queueFiles.Length} pending job(s) in queue");

            foreach (var filePath in queueFiles)
            {
                try
                {
                    using var stream = File.OpenRead(filePath);
                    var request = await JsonSerializer.DeserializeAsync(stream, MetadataJsonContext.Default.ScanJobRequest, cancellationToken);
                    
                    if (request != null)
                    {
                        jobs.Add(request);
                        _logService.LogInformation($"Loaded job request {request.JobId} from {Path.GetFileName(filePath)}");
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"Failed to load job from {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, $"Error loading pending jobs: {ex.Message}");
        }

        return jobs;
    }

    /// <summary>
    /// Deletes a job request file when the job is complete.
    /// </summary>
    public void DeleteJobRequest(Guid jobId)
    {
        string fileName = GetJobFileName(jobId);
        string filePath = Path.Combine(_queueFolderPath, fileName);

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logService.LogInformation($"Deleted job request file for {jobId}");
            }
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Failed to delete job request file for {jobId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the standardized filename for a job request.
    /// </summary>
    private static string GetJobFileName(Guid jobId)
    {
        return $"job-{jobId:N}.json";
    }

    /// <summary>
    /// Gets the path to the queue folder for diagnostic purposes.
    /// </summary>
    public string GetQueueFolderPath() => _queueFolderPath;
}
