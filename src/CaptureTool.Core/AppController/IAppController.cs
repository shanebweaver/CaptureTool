using CaptureTool.Services.Interfaces.Navigation;

namespace CaptureTool.Core.AppController;

public partial interface IAppController : INavigationHandler
{
    void Shutdown(); 
    bool TryRestart();

    string GetDefaultScreenshotsFolderPath();
    nint GetMainWindowHandle();
}
