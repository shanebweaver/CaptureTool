using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Domains.Capture.Interfaces;

public partial interface IVideoCaptureHandler
{
    event EventHandler<IVideoFile>? NewVideoCaptured;

    void StartVideoCapture(NewCaptureArgs args);
    IVideoFile StopVideoCapture();
    void CancelVideoCapture();
}
