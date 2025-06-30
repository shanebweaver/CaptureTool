using CaptureTool.Capture;
using CaptureTool.Capture.Image;
using CaptureTool.Capture.Video;
using System.Drawing;
using System.Threading.Tasks;

namespace CaptureTool.Core.AppController;

public interface IAppController
{
    void Shutdown();
    bool TryRestart();

    Task NewImageCaptureAsync(ImageCaptureOptions options);
    Task NewVideoCaptureAsync(VideoCaptureOptions options);
    Task NewAudioCaptureAsync();

    void CloseCaptureOverlays();
    void RequestCapture(MonitorCaptureResult monitor, Rectangle area);

    nint GetMainWindowHandle();

    void GoHome();
    bool TryGoBack();
    void GoBackOrHome();
}
