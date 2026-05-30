using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.Infrastructure.Abstractions.Logging;
using CaptureTool.Infrastructure.Abstractions.Storage;
using System.Text.Json;

namespace CaptureTool.Domain.Capture.Windows.Metadata;

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
    public void SaveJobRequest(ScanJobRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string fileName = GetJobFileName(request.JobId);
        string filePath = Path.Combine(_queueFolderPath, fileName);
        string tempFilePath = Path.Combine(_queueFolderPath, $"{fileName}.tmp");

        try
        {
            using (var stream = File.Create(tempFilePath))
            {
                JsonSerializer.Serialize(stream, request, MetadataJsonContext.Default.ScanJobRequest);
            }

            File.Move(tempFilePath, filePath, true);
            _logService.LogInformation($"Saved job request {request.JobId} to queue: {filePath}");
        }
        catch (Exception ex)
        {
            TryDeleteTempFile(tempFilePath);
            _logService.LogException(ex, $"Failed to save job request {request.JobId}: {ex.Message}");
            throw;
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
        string tempFilePath = Path.Combine(_queueFolderPath, $"{fileName}.tmp");

        try
        {
            await using (var stream = File.Create(tempFilePath))
            {
                await JsonSerializer.SerializeAsync(stream, request, MetadataJsonContext.Default.ScanJobRequest, cancellationToken);
            }

            File.Move(tempFilePath, filePath, true);
            _logService.LogInformation($"Saved job request {request.JobId} to queue: {filePath}");
        }
        catch (Exception ex)
        {
            TryDeleteTempFile(tempFilePath);
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

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logService.LogInformation($"Deleted job request file for {jobId}");
                }
                return;
            }
            catch (IOException ex) when (attempt < 3)
            {
                _logService.LogWarning($"Retrying delete for job request file {jobId}: {ex.Message}");
                Thread.Sleep(TimeSpan.FromMilliseconds(100 * attempt));
            }
            catch (UnauthorizedAccessException ex) when (attempt < 3)
            {
                _logService.LogWarning($"Retrying delete for job request file {jobId}: {ex.Message}");
                Thread.Sleep(TimeSpan.FromMilliseconds(100 * attempt));
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Failed to delete job request file for {jobId}: {ex.Message}");
                return;
            }
        }
    }

    private static void TryDeleteTempFile(string tempFilePath)
    {
        try
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch
        {
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
