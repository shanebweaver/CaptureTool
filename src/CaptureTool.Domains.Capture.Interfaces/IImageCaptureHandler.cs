namespace CaptureTool.Domains.Capture.Interfaces;

public partial interface IImageCaptureHandler
{
    ImageFile PerformImageCapture(NewCaptureArgs args);
    ImageFile PerformAllScreensCapture();
}
