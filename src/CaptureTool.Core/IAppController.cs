using System;
using CaptureTool.Capture.Desktop;

namespace CaptureTool.Core;

public interface IAppController
{
    event EventHandler<AppWindowPresenterAction> AppWindowPresentationUpdateRequested;

    void Shutdown();
    bool TryRestart();

    void NewDesktopCapture(DesktopCaptureOptions options);
    void NewCameraCapture();
    void NewAudioCapture();

    void UpdateAppWindowPresentation(AppWindowPresenterAction action);
}
