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

    void ShowSelectionOverlay(CaptureOptions? options = null);
    void CloseSelectionOverlay();
    void PerformImageCapture(MonitorCaptureResult monitor, Rectangle captureArea);
    void PerformAllScreensCapture();

    void ShowCaptureOverlay(MonitorCaptureResult monitor, Rectangle captureArea);
    void CloseCaptureOverlay();
    void StartVideoCapture(MonitorCaptureResult monitor, Rectangle captureArea);
    void StopVideoCapture();
    void CancelVideoCapture();

    string GetDefaultScreenshotsFolderPath();

    void HideMainWindow();
    void ShowMainWindow(bool activate = true);
    nint GetMainWindowHandle();

    void GoHome();
    bool TryGoBack();
    void GoBackOrHome();
}
