using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Domain.Capture.Abstractions.Metadata;

namespace CaptureTool.Application.Abstractions.Metadata;

public interface IRealTimeMetadataScanJob
{
    Guid JobId { get; }
    string FilePath { get; }
    MetadataScanJobStatus Status { get; }
    double Progress { get; }
    string? ErrorMessage { get; }
    string? MetadataFilePath { get; }

    event EventHandler<MetadataScanJobStatus>? StatusChanged;
    event EventHandler<double>? ProgressChanged;

    void ProcessVideoFrame(ref VideoFrameData frameData);
    void ProcessAudioSample(ref AudioSampleData sampleData);
    Task FinalizeAndSaveAsync();
    void Cancel();
}
