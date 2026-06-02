using CaptureTool.Domain.Capture.Abstractions;

namespace CaptureTool.Application.Abstractions.Features.CaptureOverlay.StartVideoCapture;

public sealed record StartVideoCaptureRequest(NewCaptureArgs CaptureArgs);
