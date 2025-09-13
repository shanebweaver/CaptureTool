using CaptureTool.Common.Storage;

namespace CaptureTool.Capture;

public partial interface IVideoCaptureHandler
{
    void StartVideoCapture(NewCaptureArgs args);
    VideoFile StopVideoCapture();
    void CancelVideoCapture();
}
