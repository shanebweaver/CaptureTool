using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Application.Abstractions.Capture;

public interface IImageCaptureHandler
{
    event EventHandler<IImageFile>? NewImageCaptured;

    ImageFile PerformAllScreensCapture();
    ImageFile PerformMultiMonitorImageCapture(MonitorCaptureResult[] monitors);
    ImageFile PerformImageCapture(NewCaptureArgs args);
}
