using CaptureTool.Capture;
using CaptureTool.Services.Activation;
using CaptureTool.Services.Navigation;

namespace CaptureTool.Core.AppController;

public partial interface IAppController : INavigationHandler, IActivationHandler, IImageCaptureHandler, IVideoCaptureHandler
{
    void Shutdown(); 
    bool TryRestart();

    string GetDefaultScreenshotsFolderPath();
    nint GetMainWindowHandle();
}
