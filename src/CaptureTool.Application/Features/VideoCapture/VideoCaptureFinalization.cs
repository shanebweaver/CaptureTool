using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Features.VideoCapture;

internal readonly record struct VideoCaptureFinalization(
    Guid SessionId,
    PendingVideoFile PendingVideo,
    bool WasPaused);
