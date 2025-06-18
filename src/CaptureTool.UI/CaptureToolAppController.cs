using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CaptureTool.Capture.Image;
using CaptureTool.Capture.Video;
using CaptureTool.Capture.Windows;
using CaptureTool.Capture.Windows.SnippingTool;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;
using CaptureTool.UI.Xaml.Windows;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Capture;
using WinUIEx;

namespace CaptureTool.UI;

internal partial class CaptureToolAppController : IAppController
{
    private readonly IFeatureManager _featureManager;
    private readonly ILogService _logService;
    private readonly INavigationService _navigationService;
    private readonly ISnippingToolService _snippingToolService;

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

        bool useSnippingTool = _snippingToolService.IsSnippingToolInstalled();
        if (useSnippingTool)
        {
            HideMainWindow();

            SnippingToolCaptureMode captureMode = ParseImageCaptureMode(options.ImageCaptureMode);
            SnippingToolCaptureOptions snippingToolOptions = new(captureMode, options.AutoSave);
            await _snippingToolService.CaptureImageAsync(snippingToolOptions);
        }
        else
        {
            var picker = new GraphicsCapturePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var captureItem = await picker.PickSingleItemAsync();
            if (captureItem == null)
            {
                return; // User canceled the picker
            }

            var storageFolder = Windows.Storage.ApplicationData.Current.TemporaryFolder;
            var fileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var file = await GraphicsCaptureHelper.CaptureItemToBitmapFileAsync(captureItem, storageFolder, fileName);

            ImageFile imageFile = new(file.Path);
            _navigationService.Navigate(CaptureToolNavigationRoutes.ImageEdit, imageFile);
        }
        //else
        //{
        //    // TODO: Call a "thing" to lookup the number of monitors and create a new window for each.

        //    if (_captureOverlayWindow == null)
        //    {
        //        _captureOverlayWindow = new();
        //        _captureOverlayWindow.Closed += ImageCaptureWindow_Closed;
        //    }

        //    _captureOverlayWindow.Activate();
        //    SetForegroundWindow(_captureOverlayWindow.GetWindowHandle());
        //}
    }

    private CaptureOverlayWindow? _captureOverlayWindow;

    //[LibraryImport("user32.dll")]
    //[return: MarshalAs(UnmanagedType.Bool)]
    //private static partial bool SetForegroundWindow(IntPtr hWnd);

    public void CloseCaptureOverlay()
    {
        _captureOverlayWindow?.Close();
    }

    private void ImageCaptureWindow_Closed(object sender, WindowEventArgs args)
    {
        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            if (_captureOverlayWindow != null)
            {
                _captureOverlayWindow.Closed -= ImageCaptureWindow_Closed;
                _captureOverlayWindow = null;
            }

            RestoreMainWindow();
        });
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
        HideMainWindow();

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

    public nint GetMainWindowHandle()
    {
        return WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
    }

    public void GoHome()
    {
        RestoreMainWindow();

        if (_navigationService.CurrentRoute != CaptureToolNavigationRoutes.Home)
        {
            _navigationService.Navigate(CaptureToolNavigationRoutes.Home, clearHistory: true);
        }
    }

    private static void HideMainWindow()
    {
        App.Current.MainWindow?.Minimize();
    }

    private static void RestoreMainWindow()
    {
        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            App.Current.MainWindow?.Restore();
            App.Current.MainWindow?.Activate();
        });
    }

    public bool TryGoBack()
    {
        if (_navigationService.CanGoBack)
        {
            RestoreMainWindow();
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

        RestoreMainWindow();
    }
}
