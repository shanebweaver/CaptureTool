using CaptureTool.Capture.Image;
using CaptureTool.Capture.Video;
using System.Threading.Tasks;

namespace CaptureTool.Core.AppController;

public interface IAppController
{
    void Shutdown();
    bool TryRestart();

    Task NewImageCaptureAsync(ImageCaptureOptions options);
    Task NewVideoCaptureAsync(VideoCaptureOptions options);
    Task NewAudioCaptureAsync();

    void CloseCaptureOverlay();

    nint GetMainWindowHandle();

    void GoHome();
    bool TryGoBack();
    void GoBackOrHome();
}
