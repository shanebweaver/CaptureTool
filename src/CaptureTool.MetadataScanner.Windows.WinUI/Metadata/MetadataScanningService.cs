using System.Collections.Concurrent;
using System.Text.Json;

namespace CaptureTool.MetadataScanner.Windows.WinUI.Metadata;

public interface IMetadataScanningService
{
    IMetadataScanJob QueueScan(string filePath);
    IReadOnlyList<IMetadataScanJob> GetActiveJobs();
    IMetadataScanJob? GetJob(Guid jobId);
    void CancelAllJobs();
}

public sealed class MetadataScanningService : IMetadataScanningService, IDisposable
{
    private readonly IMetadataScannerRegistry registry;
    private readonly PersistentJobQueueManager queueManager = new();
    private readonly ConcurrentDictionary<Guid, MetadataScanJob> activeJobs = new();
    private readonly BlockingCollection<MetadataScanJob> jobQueue = new();
    private readonly CancellationTokenSource serviceCancellation = new();
    private readonly Task processingTask;

    public MetadataScanningService(IMetadataScannerRegistry registry)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _ = Task.Run(LoadPendingJobsAsync);
        processingTask = Task.Run(ProcessJobsAsync);
    }

    public IMetadataScanJob QueueScan(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found.", filePath);
        }

        var jobId = Guid.NewGuid();
        var request = new ScanJobRequest(jobId, filePath);

        _ = Task.Run(async () => await queueManager.SaveJobRequestAsync(request));

        var job = new MetadataScanJob(jobId, filePath);
        activeJobs[job.JobId] = job;
        jobQueue.Add(job);

        return job;
    }

    public IReadOnlyList<IMetadataScanJob> GetActiveJobs()
    {
        return activeJobs.Values
            .Where(job => job.Status is MetadataScanJobStatus.Queued or MetadataScanJobStatus.Processing)
            .Cast<IMetadataScanJob>()
            .ToList();
    }

    public IMetadataScanJob? GetJob(Guid jobId)
    {
        return activeJobs.TryGetValue(jobId, out var job) ? job : null;
    }

    public void CancelAllJobs()
    {
        foreach (var job in activeJobs.Values)
        {
            job.Cancel();
        }
    }

    private async Task LoadPendingJobsAsync()
    {
        var pendingRequests = await queueManager.LoadPendingJobsAsync(serviceCancellation.Token);

        foreach (var request in pendingRequests)
        {
            if (!File.Exists(request.MediaFilePath))
            {
                queueManager.DeleteJobRequest(request.JobId);
                continue;
            }

            var job = new MetadataScanJob(request.JobId, request.MediaFilePath);
            activeJobs[job.JobId] = job;
            jobQueue.Add(job, serviceCancellation.Token);
        }
    }

    private async Task ProcessJobsAsync()
    {
        while (!serviceCancellation.Token.IsCancellationRequested)
        {
            MetadataScanJob? job = null;

            try
            {
                job = jobQueue.Take(serviceCancellation.Token);
                await ProcessJobAsync(job);
            }
            catch (OperationCanceledException) when (serviceCancellation.Token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                job?.SetError($"Unexpected error: {ex.Message}");
            }
        }
    }

    private async Task ProcessJobAsync(MetadataScanJob job)
    {
        try
        {
            job.UpdateStatus(MetadataScanJobStatus.Processing);

            var metadataEntries = new List<MetadataEntry>();
            var scannerInfo = new Dictionary<string, string>();
            var scanners = registry.GetMediaFileScanners();

            if (scanners.Count == 0)
            {
                job.UpdateProgress(50);
            }

            for (int i = 0; i < scanners.Count; i++)
            {
                IMediaFileMetadataScanner scanner = scanners[i];
                scannerInfo[scanner.ScannerId] = scanner.Name;

                IReadOnlyList<MetadataEntry> entries = await scanner.ScanFileAsync(job.FilePath, job.CancellationToken);
                metadataEntries.AddRange(entries);

                job.UpdateProgress(((i + 1d) / scanners.Count) * 90d);
            }

            var metadataFile = new MetadataFile(
                job.FilePath,
                DateTime.UtcNow,
                metadataEntries,
                scannerInfo);

            string metadataPath = Path.ChangeExtension(job.FilePath, MetadataFile.FileExtension);
            await SaveMetadataFileAsync(metadataFile, metadataPath, job.CancellationToken);

            queueManager.DeleteJobRequest(job.JobId);
            job.Complete(metadataPath);
        }
        catch (OperationCanceledException)
        {
            queueManager.DeleteJobRequest(job.JobId);
        }
        catch (Exception ex)
        {
            job.SetError(ex.Message);
        }
    }

    private static async Task SaveMetadataFileAsync(MetadataFile metadataFile, string path, CancellationToken cancellationToken)
    {
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
        serviceCancellation.Cancel();
        jobQueue.CompleteAdding();

        try
        {
            processingTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
        }

        foreach (var job in activeJobs.Values)
        {
            job.Dispose();
        }

        serviceCancellation.Dispose();
        jobQueue.Dispose();
    }
}
