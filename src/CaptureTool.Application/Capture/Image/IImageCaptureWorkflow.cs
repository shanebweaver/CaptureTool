using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Capture.Image;

internal interface IImageCaptureWorkflow
{
    event EventHandler<ImageFile>? NewImageCaptured;

    ImageFile CaptureAllScreens();
    ImageFile CaptureMonitors(IReadOnlyList<MonitorCaptureResult> monitors);
    ImageFile CaptureImage(NewCaptureArgs args);
}
