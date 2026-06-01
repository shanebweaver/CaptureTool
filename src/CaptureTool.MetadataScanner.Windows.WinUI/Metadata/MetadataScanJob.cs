namespace CaptureTool.MetadataScanner.Windows.WinUI.Metadata;

public enum MetadataScanJobStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
    Cancelled
}

public interface IMetadataScanJob
{
    Guid JobId { get; }
    string FilePath { get; }
    MetadataScanJobStatus Status { get; }
    double Progress { get; }
    string? ErrorMessage { get; }
    string? MetadataFilePath { get; }

    event EventHandler<MetadataScanJobStatus>? StatusChanged;
    event EventHandler<double>? ProgressChanged;

    void Cancel();
}

internal sealed class MetadataScanJob : IMetadataScanJob, IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private MetadataScanJobStatus status = MetadataScanJobStatus.Queued;
    private double progress;
    private string? errorMessage;
    private string? metadataFilePath;
    private bool disposed;

    public MetadataScanJob(Guid jobId, string filePath)
    {
        JobId = jobId;
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public Guid JobId { get; }
    public string FilePath { get; }

    public MetadataScanJobStatus Status
    {
        get => status;
        private set
        {
            if (status != value)
            {
                status = value;
                StatusChanged?.Invoke(this, value);
            }
        }
    }

    public double Progress
    {
        get => progress;
        private set
        {
            if (Math.Abs(progress - value) > 0.01)
            {
                progress = value;
                ProgressChanged?.Invoke(this, value);
            }
        }
    }

    public string? ErrorMessage => errorMessage;
    public string? MetadataFilePath => metadataFilePath;
    internal CancellationToken CancellationToken => cancellationTokenSource.Token;

    public event EventHandler<MetadataScanJobStatus>? StatusChanged;
    public event EventHandler<double>? ProgressChanged;

    public void Cancel()
    {
        if (Status == MetadataScanJobStatus.Queued || Status == MetadataScanJobStatus.Processing)
        {
            cancellationTokenSource.Cancel();
            Status = MetadataScanJobStatus.Cancelled;
        }
    }

    internal void UpdateStatus(MetadataScanJobStatus newStatus)
    {
        Status = newStatus;
    }

    internal void UpdateProgress(double newProgress)
    {
        Progress = Math.Clamp(newProgress, 0, 100);
    }

    internal void SetError(string message)
    {
        errorMessage = message;
        Status = MetadataScanJobStatus.Failed;
    }

    internal void Complete(string path)
    {
        metadataFilePath = path;
        Progress = 100;
        Status = MetadataScanJobStatus.Completed;
    }

    public void Dispose()
    {
        if (!disposed)
        {
            cancellationTokenSource.Dispose();
            disposed = true;
        }
    }
}
