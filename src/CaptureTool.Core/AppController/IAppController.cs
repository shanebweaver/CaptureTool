using CaptureTool.Capture;
using System.Drawing;

namespace CaptureTool.Core.AppController;

public interface IAppController
{
    void Shutdown();
    bool TryRestart();

    void ShowCaptureOverlay(CaptureOptions? options = null);
    void CloseCaptureOverlay();
    void PerformCapture(MonitorCaptureResult monitor, Rectangle captureArea);

    void RestoreMainWindow();
    void HideMainWindow();
    void ShowMainWindow();
    nint GetMainWindowHandle();

    void GoHome();
    bool TryGoBack();
    void GoBackOrHome();
}
