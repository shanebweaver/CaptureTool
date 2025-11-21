namespace CaptureTool.Capture;

public partial interface IImageCaptureHandler
{
    ImageFile PerformImageCapture(NewCaptureArgs args);
    ImageFile PerformAllScreensCapture();
}
