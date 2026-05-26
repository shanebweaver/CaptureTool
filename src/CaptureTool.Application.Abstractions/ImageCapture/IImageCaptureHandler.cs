using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.Abstractions.ImageCapture;

public partial interface IImageCaptureHandler
{
    event EventHandler<IImageFile>? NewImageCaptured;

    ImageFile PerformImageCapture(NewCaptureArgs args);
    ImageFile PerformMultiMonitorImageCapture(MonitorCaptureResult[] monitors);
    ImageFile PerformAllScreensCapture();
}
