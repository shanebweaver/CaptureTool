using System;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop;

namespace CaptureTool.Core;

public interface IAppController
{
    event EventHandler<AppWindowPresenterAction> AppWindowPresentationUpdateRequested;

    void Shutdown();
    bool TryRestart();

    Task NewDesktopCaptureAsync(DesktopCaptureOptions options);
    Task NewCameraCaptureAsync();
    Task NewAudioCaptureAsync();

    void UpdateAppWindowPresentation(AppWindowPresenterAction action);

    nint GetMainWindowHandle();

    void NavigateHome();
}
