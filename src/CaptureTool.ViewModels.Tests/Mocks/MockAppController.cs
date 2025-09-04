using CaptureTool.Capture;
using CaptureTool.Core.AppController;
using System;
using System.Drawing;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels.Tests.Mocks;

internal sealed partial class MockAppController : IAppController
{
    public void CloseSelectionOverlay()
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

    public void GoBackOrHome()
    {
    }

    public void GoHome()
    {
    }

    public Task HandleLaunchActicationAsync()
    {
        return Task.CompletedTask;
    }

    public Task HandleProtocolActivationAsync(Uri protocolUri)
    {
        return Task.CompletedTask;
    }

    public void HideMainWindow()
    {
    }

    public void PerformAllScreensCapture()
    {
    }

    public void PerformCapture(MonitorCaptureResult monitor, Rectangle captureArea)
    {
    }

    public void PerformImageCapture(MonitorCaptureResult monitor, Rectangle captureArea)
    {
    }

    public void PrepareForVideoCapture(MonitorCaptureResult monitor, Rectangle captureArea)
    {
    }

    public void ShowSelectionOverlay(CaptureOptions? options = null)
    {
    }

    public void ShowMainWindow(bool activate = true)
    {
    }

    public void Shutdown()
    {
    }

    public void StartVideoCapture(MonitorCaptureResult monitor, Rectangle captureArea)
    {
    }

    public bool TryGoBack()
    {
        return false;
    }

    public bool TryRestart()
    {
        return false;
    }
}
