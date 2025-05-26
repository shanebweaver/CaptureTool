using System;
using System.Threading.Tasks;
using CaptureTool.Capture.Image;
using CaptureTool.Capture.Video;

namespace CaptureTool.Core.AppController;

public interface IAppController
{
    event EventHandler<AppWindowPresenterAction> AppWindowPresentationUpdateRequested;

    void Shutdown();
    bool TryRestart();

    Task NewImageCaptureAsync(ImageCaptureOptions options);
    Task NewVideoCaptureAsync(VideoCaptureOptions options);
    Task NewAudioCaptureAsync();

    void UpdateAppWindowPresentation(AppWindowPresenterAction action);

    nint GetMainWindowHandle();

    void GoHome();
    bool TryGoBack();
    void GoBackOrHome();
}
