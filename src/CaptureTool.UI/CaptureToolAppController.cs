using CaptureTool.Capture.Image;
using CaptureTool.Capture.Video;
using CaptureTool.Capture.Windows.SnippingTool;
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

    public async Task NewImageCaptureAsync(ImageCaptureOptions options)
    {
        // Feature check
        bool isCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Capture);
        bool isImageCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Capture_Image);
        if (!isCaptureEnabled || !isImageCaptureEnabled)
        {
            throw new InvalidOperationException("Feature is not enabled");
        }

        // Show loading screen and minimize
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
            App.Current.DispatcherQueue.TryEnqueue(() =>
            {
                ImageCaptureWindow imageCaptureWindow = new();
                imageCaptureWindow.Activate();
            });
        }
    }

    public async Task NewVideoCaptureAsync(VideoCaptureOptions options)
    {
        // Feature check
        bool isCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Capture);
        bool isVideoCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Capture_Video);
        if (!isCaptureEnabled || !isVideoCaptureEnabled)
        {
            throw new InvalidOperationException("Feature is not enabled");
        }

        // Show loading screen and minimize
        UpdateAppWindowPresentation(AppWindowPresenterAction.Minimize);

        SnippingToolCaptureMode captureMode = ParseVideoCaptureMode(options.VideoCaptureMode);
        SnippingToolCaptureOptions snippingToolOptions = new(captureMode, options.AutoSave);
        await _snippingToolService.CaptureVideoAsync(snippingToolOptions);
    }

    private static SnippingToolCaptureMode ParseImageCaptureMode(ImageCaptureMode? imageCaptureMode)
    {
        SnippingToolCaptureMode snippingToolCaptureMode = imageCaptureMode switch
        {
            ImageCaptureMode.Rectangle => SnippingToolCaptureMode.Rectangle,
            ImageCaptureMode.Window => SnippingToolCaptureMode.Window,
            ImageCaptureMode.Fullscreen => SnippingToolCaptureMode.Fullscreen,
            ImageCaptureMode.Freeform => SnippingToolCaptureMode.Freeform,
            _ => throw new ArgumentOutOfRangeException(nameof(imageCaptureMode), "Unexpected image capture mode.")
        };
        return snippingToolCaptureMode;
    }

    private static SnippingToolCaptureMode ParseVideoCaptureMode(VideoCaptureMode? videoCaptureMode)
    {
        SnippingToolCaptureMode snippingToolCaptureMode = videoCaptureMode switch
        {
            VideoCaptureMode.Rectangle => SnippingToolCaptureMode.Rectangle,
            _ => throw new ArgumentOutOfRangeException(nameof(videoCaptureMode), "Unexpected video capture mode.")
        };
        return snippingToolCaptureMode;
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

    public void GoHome()
    {
        UpdateAppWindowPresentation(AppWindowPresenterAction.Restore);

        if (_navigationService.CurrentRoute != CaptureToolNavigationRoutes.Home)
        {
            _navigationService.Navigate(CaptureToolNavigationRoutes.Home, clearHistory: true);
        }
    }

    public bool TryGoBack()
    {
        if (_navigationService.CanGoBack)
        {
            UpdateAppWindowPresentation(AppWindowPresenterAction.Restore);
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
