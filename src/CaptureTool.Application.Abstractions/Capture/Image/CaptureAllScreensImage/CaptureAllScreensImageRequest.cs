using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Capture.Image.CaptureAllScreensImage;

public sealed record CaptureAllScreensImageRequest(IReadOnlyList<MonitorCaptureResult> Monitors);
