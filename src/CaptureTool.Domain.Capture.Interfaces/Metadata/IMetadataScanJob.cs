namespace CaptureTool.Domain.Capture.Interfaces.Metadata;

/// <summary>
/// Status of a metadata scan job.
/// </summary>
public enum MetadataScanJobStatus
{
    /// <summary>
    /// Job is queued and waiting to start.
    /// </summary>
    Queued,

    /// <summary>
    /// Job is currently processing.
    /// </summary>
    Processing,

    /// <summary>
    /// Job completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Job failed with an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Job was cancelled.
    /// </summary>
    Cancelled
}

/// <summary>
/// Represents a metadata scanning job with progress tracking.
/// </summary>
public interface IMetadataScanJob
{
    /// <summary>
    /// Gets the unique identifier for this scan job.
    /// </summary>
    Guid JobId { get; }

    /// <summary>
    /// Gets the file path being scanned.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Gets the current status of the job.
    /// </summary>
    MetadataScanJobStatus Status { get; }

    /// <summary>
    /// Gets the progress percentage (0-100).
    /// </summary>
    double Progress { get; }

    /// <summary>
    /// Gets the error message if the job failed.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Gets the path to the generated metadata file, if completed.
    /// </summary>
    string? MetadataFilePath { get; }

    /// <summary>
    /// Event raised when the job status changes.
    /// </summary>
    event EventHandler<MetadataScanJobStatus>? StatusChanged;

    /// <summary>
    /// Event raised when progress is updated.
    /// </summary>
    event EventHandler<double>? ProgressChanged;

    /// <summary>
    /// Cancels the job if it's still queued or processing.
    /// </summary>
    void Cancel();
}
