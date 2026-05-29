using CaptureTool.Domain.Capture.Abstractions;

namespace CaptureTool.Application.Features.CaptureOverlay.StartVideoCapture;

public sealed record StartVideoCaptureRequest(NewCaptureArgs CaptureArgs);
