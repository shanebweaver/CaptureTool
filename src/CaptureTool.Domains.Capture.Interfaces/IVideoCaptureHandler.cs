namespace CaptureTool.Domains.Capture.Interfaces;

public partial interface IVideoCaptureHandler
{
    void StartVideoCapture(NewCaptureArgs args);
    VideoFile StopVideoCapture();
    void CancelVideoCapture();
}
