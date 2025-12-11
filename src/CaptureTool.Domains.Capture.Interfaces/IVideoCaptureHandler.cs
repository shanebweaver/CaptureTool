using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Domains.Capture.Interfaces;

public partial interface IVideoCaptureHandler
{
    event EventHandler<IVideoFile>? NewVideoCaptured;
    event EventHandler<bool>? DesktopAudioStateChanged;

    bool IsCapturing { get; }
    bool IsDesktopAudioEnabled { get; }
    void SetIsDesktopAudioEnabled(bool value);

    void StartVideoCapture(NewCaptureArgs args);
    IVideoFile StopVideoCapture();
    void CancelVideoCapture();
}
