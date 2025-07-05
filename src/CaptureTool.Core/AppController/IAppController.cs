using System.Drawing;

namespace CaptureTool.Core.AppController;

public interface IAppController
{
    void Shutdown();
    bool TryRestart();

    void ShowCaptureOverlay();
    void CloseCaptureOverlay();
    void RequestCapture(nint hMonitor, Rectangle area);

    void RestoreMainWindow();
    nint GetMainWindowHandle();

    void GoHome();
    bool TryGoBack();
    void GoBackOrHome();
}
