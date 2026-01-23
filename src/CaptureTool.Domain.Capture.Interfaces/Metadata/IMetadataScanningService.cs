namespace CaptureTool.Domain.Capture.Interfaces.Metadata;

/// <summary>
/// Service for managing metadata scanning jobs asynchronously.
/// </summary>
public interface IMetadataScanningService
{
    /// <summary>
    /// Queues a metadata scan job for the specified video file.
    /// </summary>
    /// <param name="filePath">Path to the video file to scan.</param>
    /// <returns>The created scan job for tracking.</returns>
    IMetadataScanJob QueueScan(string filePath);

    /// <summary>
    /// Gets all active scan jobs.
    /// </summary>
    IReadOnlyList<IMetadataScanJob> GetActiveJobs();

    /// <summary>
    /// Gets a specific job by its ID.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <returns>The job if found, null otherwise.</returns>
    IMetadataScanJob? GetJob(Guid jobId);

    /// <summary>
    /// Cancels all active jobs.
    /// </summary>
    void CancelAllJobs();
}
