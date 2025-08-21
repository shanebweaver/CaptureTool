using CaptureTool.Capture;
using System;
using System.Drawing;
using System.Threading.Tasks;

namespace CaptureTool.Core.AppController;

public interface IAppController
{
    Task HandleLaunchActicationAsync();
    Task HandleProtocolActivationAsync(Uri protocolUri);

    void Shutdown(); 
    bool TryRestart();

    void ShowCaptureOverlay(CaptureOptions? options = null);
    void CloseCaptureOverlay();
    void PerformImageCapture(MonitorCaptureResult monitor, Rectangle captureArea);
    void PrepareForVideoCapture(MonitorCaptureResult monitor, Rectangle captureArea);
    void PerformAllScreensCapture();

    string GetDefaultScreenshotsFolderPath();

    void HideMainWindow();
    void ShowMainWindow(bool activate = true);
    nint GetMainWindowHandle();

    void GoHome();
    bool TryGoBack();
    void GoBackOrHome();
}
