using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture.Video;

internal readonly record struct VideoCaptureFinalization(
    Guid SessionId,
    PendingVideoFile PendingVideo,
    bool WasPaused);
