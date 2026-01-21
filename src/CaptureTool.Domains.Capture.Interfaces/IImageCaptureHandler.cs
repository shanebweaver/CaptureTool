using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Domains.Capture.Interfaces;

public partial interface IImageCaptureHandler
{
    event EventHandler<IImageFile>? NewImageCaptured;

    ImageFile PerformImageCapture(NewCaptureArgs args);
    ImageFile PerformMultiMonitorImageCapture(MonitorCaptureResult[] monitors);
    ImageFile PerformAllScreensCapture();
}
