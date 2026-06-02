using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Domain.Capture.Abstractions.Files;

namespace CaptureTool.Application.Abstractions.Capture;

public interface IImageCaptureHandler
{
    event EventHandler<IImageFile>? NewImageCaptured;

    ImageFile PerformAllScreensCapture();
    ImageFile PerformMultiMonitorImageCapture(MonitorCaptureResult[] monitors);
    ImageFile PerformImageCapture(NewCaptureArgs args);
}
