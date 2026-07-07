using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Capture.Video.StartVideoCapture;

public sealed record StartVideoCaptureRequest(NewCaptureArgs CaptureArgs);
