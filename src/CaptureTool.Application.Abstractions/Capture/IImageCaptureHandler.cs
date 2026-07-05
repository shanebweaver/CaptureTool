using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Abstractions.Capture;

public interface IImageCaptureHandler
{
    event EventHandler<ImageFile>? NewImageCaptured;

    ImageFile PerformAllScreensCapture();
    ImageFile PerformMultiMonitorImageCapture(MonitorCaptureResult[] monitors);
    ImageFile PerformImageCapture(NewCaptureArgs args);
}
