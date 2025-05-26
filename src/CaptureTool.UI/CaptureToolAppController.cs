using CaptureTool.Capture.Desktop;
using CaptureTool.Capture.Desktop.SnippingTool;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;
using CaptureTool.UI.Xaml.Windows;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;

namespace CaptureTool.UI;

internal class CaptureToolAppController : IAppController
{
    private readonly IFeatureManager _featureManager;
    private readonly ILogService _logService;
    private readonly INavigationService _navigationService;
    private readonly ISnippingToolService _snippingToolService;

    private DesktopImageCaptureWindow? _desktopImageCaptureWindow;

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

        _snippingToolService.ResponseReceived += OnSnippingToolResponseReceived;
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

    private void CleanupDesktopImageCaptureWindow()
    {
        if (_desktopImageCaptureWindow != null)
        {
            _desktopImageCaptureWindow.Close();
            _desktopImageCaptureWindow = null;
            UpdateAppWindowPresentation(AppWindowPresenterAction.Restore);
        }
    }

    public async Task NewDesktopImageCaptureAsync(DesktopImageCaptureOptions options)
    {
        // Feature check
        bool isDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture);
        bool isImageCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Image);
        if (!isDesktopCaptureEnabled || !isImageCaptureEnabled)
        {
            throw new InvalidOperationException("Feature is not enabled");
        }

        CleanupDesktopImageCaptureWindow();

        // Show loading screen and minimize
        _navigationService.Navigate(CaptureToolNavigationRoutes.Loading, null);
        UpdateAppWindowPresentation(AppWindowPresenterAction.Minimize);

        bool useSnippingTool = false;
        if (useSnippingTool)
        {
            SnippingToolCaptureMode captureMode = ParseImageCaptureMode(options.ImageCaptureMode);
            SnippingToolCaptureOptions snippingToolOptions = new(captureMode, options.AutoSave);
            await _snippingToolService.CaptureImageAsync(snippingToolOptions);
        }
        else
        {
            _desktopImageCaptureWindow = new();
            _desktopImageCaptureWindow.Activate();
        }
    }

    public async Task NewDesktopVideoCaptureAsync(DesktopVideoCaptureOptions options)
    {
        // Feature check
        bool isDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture);
        bool isVideoCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Video);
        if (!isDesktopCaptureEnabled || !isVideoCaptureEnabled)
        {
            throw new InvalidOperationException("Feature is not enabled");
        }

        CleanupDesktopImageCaptureWindow();

        // Show loading screen and minimize
        _navigationService.Navigate(CaptureToolNavigationRoutes.Loading, null);
        UpdateAppWindowPresentation(AppWindowPresenterAction.Minimize);

        SnippingToolCaptureMode captureMode = ParseVideoCaptureMode(options.VideoCaptureMode);
        SnippingToolCaptureOptions snippingToolOptions = new(captureMode, options.AutoSave);
        await _snippingToolService.CaptureVideoAsync(snippingToolOptions);
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

    public Task NewDesktopAudioCaptureAsync()
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

    public void GoHome()
    {
        CleanupDesktopImageCaptureWindow();

        if (_navigationService.CurrentRoute != CaptureToolNavigationRoutes.Home)
        {
            _navigationService.Navigate(CaptureToolNavigationRoutes.Home, clearHistory: true);
        }
    }

    public bool TryGoBack()
    {
        if (_navigationService.CanGoBack)
        {
            CleanupDesktopImageCaptureWindow();
            _navigationService.GoBack();
            return true;
        }

        return false;
    }

    public void GoBackOrHome()
    {
        if (!TryGoBack())
        {
            GoHome();
        }
    }

    private async void OnSnippingToolResponseReceived(object? sender, SnippingToolResponse e)
    {
        Debug.WriteLine($"SnippingToolResponse: {e.Code} - {e.Reason}");
        UpdateAppWindowPresentation(AppWindowPresenterAction.Restore);

        if (e.Code == 200)
        {
            try
            {
                var file = await e.GetFileAsync();
                string mimeType = file.ContentType;
                if (mimeType.StartsWith("image"))
                {
                    ImageFile imageFile = new(file.Path);
                    _navigationService.Navigate(CaptureToolNavigationRoutes.ImageEdit, imageFile);
                }
                else if (mimeType.StartsWith("video"))
                {
                    VideoFile videoFile = new(file.Path);
                    _navigationService.Navigate(CaptureToolNavigationRoutes.VideoEdit, videoFile);
                }
            }
            catch (Exception)
            {
                GoHome();
            }
        }
        else
        {
            GoHome();
        }
    }
}
