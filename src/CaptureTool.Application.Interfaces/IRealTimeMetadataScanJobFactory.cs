using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Domain.Capture.Interfaces.Metadata;
using CaptureTool.Domain.Capture.Interfaces.Metadata.Processing;

namespace CaptureTool.Application.Interfaces;

/// <summary>
/// Factory for creating real-time metadata scan jobs during video capture.
/// </summary>
public interface IRealTimeMetadataScanJobFactory
{
    /// <summary>
    /// Creates a new real-time metadata scan job.
    /// </summary>
    /// <param name="jobId">Unique identifier for the job.</param>
    /// <param name="filePath">Path to the video file being recorded.</param>
    /// <param name="registry">Scanner registry to use.</param>
    /// <param name="processingPipeline">
    /// Optional processing pipeline. When provided the job will run Layer 2 processing after
    /// finalizing raw metadata and save a <see cref="RefinedMetadataFile"/> alongside it.
    /// </param>
    /// <returns>A new metadata scan job that can process frames/samples in real-time.</returns>
    IRealTimeMetadataScanJob CreateJob(
        Guid jobId,
        string filePath,
        IMetadataScannerRegistry registry,
        IMetadataProcessingPipeline? processingPipeline = null);
}

/// <summary>
/// Extends IMetadataScanJob with real-time processing capabilities.
/// </summary>
public interface IRealTimeMetadataScanJob : IMetadataScanJob
{
    /// <summary>
    /// Processes a video frame.
    /// </summary>
    void ProcessVideoFrame(ref VideoFrameData frameData);

    /// <summary>
    /// Processes an audio sample.
    /// </summary>
    void ProcessAudioSample(ref AudioSampleData sampleData);

    /// <summary>
    /// Finalizes the metadata collection and saves to disk.
    /// </summary>
    Task FinalizeAndSaveAsync();
}
