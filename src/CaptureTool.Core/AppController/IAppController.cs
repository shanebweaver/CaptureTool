using CaptureTool.Capture;
using CaptureTool.Services.Activation;
using CaptureTool.Services.Navigation;

namespace CaptureTool.Core.AppController;

public partial interface IAppController : INavigationHandler, IActivationHandler, IImageCaptureHandler, IVideoCaptureHandler
{
    void Shutdown(); 
    bool TryRestart();

    //void ShowSelectionOverlay(CaptureOptions? options = null);
    //void CloseSelectionOverlay();
    //void PerformImageCapture(MonitorCaptureResult monitor, Rectangle captureArea);
    //void PerformAllScreensCapture();

    //void ShowCaptureOverlay(MonitorCaptureResult monitor, Rectangle captureArea);
    //void CloseCaptureOverlay();
    //void StartVideoCapture(MonitorCaptureResult monitor, Rectangle captureArea);
    //void StopVideoCapture();
    //void CancelVideoCapture();

    string GetDefaultScreenshotsFolderPath();

    //void HideMainWindow();
    //void ShowMainWindow(bool activate = true);
    nint GetMainWindowHandle();

    //void GoHome();
    //bool TryGoBack();
    //void GoBackOrHome();
}
