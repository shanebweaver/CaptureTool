using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop;
using CaptureTool.Core;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;
using CaptureTool.Capture.Desktop.SnippingTool;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Core;

namespace CaptureTool.UI;

internal class CaptureToolAppController : IAppController
{
    private readonly IFeatureManager _featureManager;
    private readonly ILogService _logService;
    private readonly INavigationService _navigationService;
    private readonly ISnippingToolService _snippingToolService;

    public event EventHandler<AppWindowPresenterAction>? AppWindowPresentationUpdateRequested;

    public CaptureToolAppController(
        IFeatureManager featureManager,
        ILogService logService,
        INavigationService navigationService,
        ISnippingToolService snippingToolService) 
    {
        _featureManager = featureManager;
        _logService = logService;
        _navigationService = navigationService;
        _snippingToolService = snippingToolService;
    }

    public bool TryRestart()
    {
        AppRestartFailureReason restartError = AppInstance.Restart(string.Empty);

        switch (restartError)
        {
            case AppRestartFailureReason.NotInForeground:
                _logService.LogWarning("The app is not in the foreground.");
                break;
            case AppRestartFailureReason.RestartPending:
                _logService.LogWarning("Another restart is currently pending.");
                break;
            case AppRestartFailureReason.InvalidUser:
                _logService.LogWarning("Current user is not signed in or not a valid user.");
                break;
            case AppRestartFailureReason.Other:
                _logService.LogWarning("Failure restarting.");
                break;
        }

        return false;
    }

    public void Shutdown()
    {
        App.Current.Shutdown();
    }

    public async Task NewDesktopCaptureAsync(DesktopCaptureOptions options)
    {
        // Feature check
        bool isDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture);
        bool isImageCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Image);
        bool isVideoCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Video);
        if (!isDesktopCaptureEnabled
            || (options.CaptureMode == DesktopCaptureMode.Image && !isImageCaptureEnabled)
            || (options.CaptureMode == DesktopCaptureMode.Video && !isVideoCaptureEnabled))
        {
            Trace.Fail("Feature is not enabled");
        }

        // Show loading screen and minimize
        _navigationService.Navigate(NavigationRoutes.Loading, null);
        UpdateAppWindowPresentation(AppWindowPresenterAction.Minimize);

        if (options.CaptureMode == DesktopCaptureMode.Image)
        {
            SnippingToolCaptureMode captureMode = ParseImageCaptureMode(options.ImageCaptureMode);
            SnippingToolCaptureOptions snippingToolOptions = new(captureMode, options.AutoSave);
            await _snippingToolService.CaptureImageAsync(snippingToolOptions);
        }
        else if (options.CaptureMode == DesktopCaptureMode.Video)
        {
            SnippingToolCaptureMode captureMode = ParseVideoCaptureMode(options.VideoCaptureMode);
            SnippingToolCaptureOptions snippingToolOptions = new(captureMode, options.AutoSave);
            await _snippingToolService.CaptureVideoAsync(snippingToolOptions);
        }
    }

    private static SnippingToolCaptureMode ParseImageCaptureMode(DesktopImageCaptureMode? desktopImageCaptureMode)
    {
        SnippingToolCaptureMode snippingToolCaptureMode = desktopImageCaptureMode switch
        {
            DesktopImageCaptureMode.Rectangle => SnippingToolCaptureMode.Rectangle,
            DesktopImageCaptureMode.Window => SnippingToolCaptureMode.Window,
            DesktopImageCaptureMode.Fullscreen => SnippingToolCaptureMode.Fullscreen,
            DesktopImageCaptureMode.Freeform => SnippingToolCaptureMode.Freeform,
            _ => throw new ArgumentOutOfRangeException(nameof(desktopImageCaptureMode), "Unexpected image capture mode.")
        };
        return snippingToolCaptureMode;
    }

    private static SnippingToolCaptureMode ParseVideoCaptureMode(DesktopVideoCaptureMode? desktopVideoCaptureMode)
    {
        SnippingToolCaptureMode snippingToolCaptureMode = desktopVideoCaptureMode switch
        {
            DesktopVideoCaptureMode.Rectangle => SnippingToolCaptureMode.Rectangle,
            _ => throw new ArgumentOutOfRangeException(nameof(desktopVideoCaptureMode), "Unexpected video capture mode.")
        };
        return snippingToolCaptureMode;
    }

    public Task NewCameraCaptureAsync()
    {
        throw new NotImplementedException();
    }

    public Task NewAudioCaptureAsync()
    {
        throw new NotImplementedException();
    }

    public void UpdateAppWindowPresentation(AppWindowPresenterAction action)
    {
        AppWindowPresentationUpdateRequested?.Invoke(this, action);
    }

    public nint GetMainWindowHandle()
    {
        return WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
    }

    public void NavigateHome()
    {
        if (_navigationService.CurrentRoute != NavigationRoutes.Home)
        {
            _navigationService.Navigate(NavigationRoutes.Home, clearHistory: true);
        }
    }
}
