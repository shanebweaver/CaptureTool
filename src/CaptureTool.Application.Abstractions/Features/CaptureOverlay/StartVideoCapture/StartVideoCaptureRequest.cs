using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Features.CaptureOverlay.StartVideoCapture;

public sealed record StartVideoCaptureRequest(NewCaptureArgs CaptureArgs);
