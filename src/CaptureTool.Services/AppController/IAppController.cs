using System;

namespace CaptureTool.Services.AppController;

public interface IAppController
{
    event EventHandler<AppWindowPresenterAction> AppWindowPresentationUpdateRequested;

    void Shutdown();
    bool TryRestart();

    void NewDesktopCapture();
    void NewVideoCapture();
    void NewAudioCapture();

    void UpdateAppWindowPresentation(AppWindowPresenterAction action);
}
