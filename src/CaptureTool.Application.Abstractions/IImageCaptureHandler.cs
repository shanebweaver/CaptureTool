using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.Abstractions;

public interface IImageCaptureHandler
{
    event EventHandler<IImageFile>? NewImageCaptured;

    ImageFile PerformAllScreensCapture();
    ImageFile PerformMultiMonitorImageCapture(MonitorCaptureResult[] monitors);
    ImageFile PerformImageCapture(NewCaptureArgs args);
}
