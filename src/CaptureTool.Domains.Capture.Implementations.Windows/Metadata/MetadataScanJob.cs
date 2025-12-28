using CaptureTool.Domains.Capture.Interfaces.Metadata;

namespace CaptureTool.Domains.Capture.Implementations.Windows.Metadata;

/// <summary>
/// Implementation of a metadata scan job with progress tracking.
/// </summary>
internal sealed class MetadataScanJob : IMetadataScanJob, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private MetadataScanJobStatus _status = MetadataScanJobStatus.Queued;
    private double _progress;
    private string? _errorMessage;
    private string? _metadataFilePath;
    private bool _disposed;

    public Guid JobId { get; }
    public string FilePath { get; }

    public MetadataScanJobStatus Status
    {
        get => _status;
        private set
        {
            if (_status != value)
            {
                _status = value;
                StatusChanged?.Invoke(this, value);
            }
        }
    }

    public double Progress
    {
        get => _progress;
        private set
        {
            if (Math.Abs(_progress - value) > 0.01)
            {
                _progress = value;
                ProgressChanged?.Invoke(this, value);
            }
        }
    }

    public string? ErrorMessage => _errorMessage;
    public string? MetadataFilePath => _metadataFilePath;
    internal CancellationToken CancellationToken => _cancellationTokenSource.Token;

    public event EventHandler<MetadataScanJobStatus>? StatusChanged;
    public event EventHandler<double>? ProgressChanged;

    public MetadataScanJob(Guid jobId, string filePath)
    {
        JobId = jobId;
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public void Cancel()
    {
        if (Status == MetadataScanJobStatus.Queued || Status == MetadataScanJobStatus.Processing)
        {
            _cancellationTokenSource.Cancel();
            Status = MetadataScanJobStatus.Cancelled;
        }
    }

    internal void UpdateStatus(MetadataScanJobStatus status)
    {
        Status = status;
    }

    internal void UpdateProgress(double progress)
    {
        Progress = Math.Clamp(progress, 0, 100);
    }

    internal void SetError(string errorMessage)
    {
        _errorMessage = errorMessage;
        Status = MetadataScanJobStatus.Failed;
    }

    internal void Complete(string metadataFilePath)
    {
        _metadataFilePath = metadataFilePath;
        Progress = 100;
        Status = MetadataScanJobStatus.Completed;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cancellationTokenSource.Dispose();
            _disposed = true;
        }
    }
}
