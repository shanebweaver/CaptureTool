using CaptureTool.Domain.Capture;

namespace CaptureTool.Presentation.Features.CaptureOverlay;

public readonly partial struct CaptureOverlayViewModelOptions(NewCaptureArgs captureArgs)
{
    public NewCaptureArgs CaptureArgs { get; } = captureArgs;
}
