using CaptureTool.Capture;
using CaptureTool.Common.Storage;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Navigation;
using System;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels.Tests.Mocks;

internal sealed partial class MockAppController : IAppController
{
    public void CancelVideoCapture()
    {
    }

    public string GetDefaultScreenshotsFolderPath()
    {
        return "DefaultScreenshotsFolderPath";
    }

    public nint GetMainWindowHandle()
    {
        return 0;
    }

    public Task HandleLaunchActivationAsync()
    {
        return Task.CompletedTask;
    }

    public void HandleNavigationRequest(NavigationRequest request)
    {
    }

    public Task HandleProtocolActivationAsync(Uri protocolUri)
    {
        return Task.CompletedTask;
    }

    public ImageFile PerformAllScreensCapture()
    {
        return new ImageFile("path-to-file");
    }

    public ImageFile PerformImageCapture(NewCaptureArgs args)
    {
        return new ImageFile("path-to-file");
    }

    public void Shutdown()
    {
    }

    public void StartVideoCapture(NewCaptureArgs args)
    {
    }

    public VideoFile StopVideoCapture()
    {
        return new VideoFile("path-to-file");
    }

    public bool TryRestart()
    {
        return false;
    }
}
