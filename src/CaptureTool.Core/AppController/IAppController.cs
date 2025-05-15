using System;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop;

namespace CaptureTool.Core.AppController;

public interface IAppController
{
    event EventHandler<AppWindowPresenterAction> AppWindowPresentationUpdateRequested;

    void Shutdown();
    bool TryRestart();

    Task NewDesktopImageCaptureAsync(DesktopImageCaptureOptions options);
    Task NewDesktopVideoCaptureAsync(DesktopVideoCaptureOptions options);
    Task NewDesktopAudioCaptureAsync();

    void UpdateAppWindowPresentation(AppWindowPresenterAction action);

    nint GetMainWindowHandle();
}
