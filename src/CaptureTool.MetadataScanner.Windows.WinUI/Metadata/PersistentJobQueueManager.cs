using System.Text.Json;

namespace CaptureTool.MetadataScanner.Windows.WinUI.Metadata;

internal sealed class PersistentJobQueueManager
{
    private readonly string queueFolderPath;

    public PersistentJobQueueManager()
    {
        queueFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CaptureTool.MetadataScanner",
            "ScanJobQueue");

        EnsureQueueFolderExists();
    }

    public async Task SaveJobRequestAsync(ScanJobRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string filePath = Path.Combine(queueFolderPath, GetJobFileName(request.JobId));
        using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, request, MetadataJsonContext.Default.ScanJobRequest, cancellationToken);
    }

    public async Task<List<ScanJobRequest>> LoadPendingJobsAsync(CancellationToken cancellationToken = default)
    {
        EnsureQueueFolderExists();

        var jobs = new List<ScanJobRequest>();
        foreach (string filePath in Directory.GetFiles(queueFolderPath, "*.json"))
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var request = await JsonSerializer.DeserializeAsync(stream, MetadataJsonContext.Default.ScanJobRequest, cancellationToken);
                if (request is not null)
                {
                    jobs.Add(request);
                }
            }
            catch
            {
                // Leave malformed queue files in place for manual diagnosis.
            }
        }

        return jobs;
    }

    public void DeleteJobRequest(Guid jobId)
    {
        string filePath = Path.Combine(queueFolderPath, GetJobFileName(jobId));
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private void EnsureQueueFolderExists()
    {
        Directory.CreateDirectory(queueFolderPath);
    }

    private static string GetJobFileName(Guid jobId)
    {
        return $"job-{jobId:N}.json";
    }
}
