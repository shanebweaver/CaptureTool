using CaptureTool.Capture;
using CaptureTool.Capture.Image;
using CaptureTool.Capture.Video;
using CaptureTool.Capture.Windows;
using CaptureTool.Capture.Windows.SnippingTool;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;
using CaptureTool.Storage;
using CaptureTool.UI.Windows.Xaml.Extensions;
using CaptureTool.UI.Windows.Xaml.Windows;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Win32;

namespace CaptureTool.UI.Windows;

internal partial class CaptureToolAppController : IAppController
{
    private readonly IFeatureManager _featureManager;
    private readonly ILogService _logService;
    private readonly INavigationService _navigationService;
    private readonly ISnippingToolService _snippingToolService;

    private readonly Dictionary<IntPtr, CaptureOverlayWindow> _captureOverlayWindows = [];

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
        bool isCaptureEnabled = _featureManager.IsEnabled(CaptureToolFeatures.Feature_Capture);
        bool isImageCaptureEnabled = _featureManager.IsEnabled(CaptureToolFeatures.Feature_Capture_Image);
        if (!isCaptureEnabled || !isImageCaptureEnabled)
        {
            throw new InvalidOperationException("Feature is not enabled");
        }

        bool useCustomOverlay = true;
        bool useSnippingTool = _snippingToolService.IsSnippingToolInstalled();
        if (useCustomOverlay)
        {
            ShowCaptureOverlayOnAllMonitors();
        }
        else if (useSnippingTool)
        {
            HideMainWindow();

            SnippingToolCaptureMode captureMode = ParseImageCaptureMode(options.ImageCaptureMode);
            SnippingToolCaptureOptions snippingToolOptions = new(captureMode, options.AutoSave);
            await _snippingToolService.CaptureImageAsync(snippingToolOptions);
        }
        else
        {
            var picker = new global::Windows.Graphics.Capture.GraphicsCapturePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var captureItem = await picker.PickSingleItemAsync();
            if (captureItem == null)
            {
                return; // User canceled the picker
            }

            var storageFolder = global::Windows.Storage.ApplicationData.Current.TemporaryFolder;
            var fileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var file = await GraphicsCaptureHelper.CaptureItemToBitmapFileAsync(captureItem, storageFolder, fileName);

            ImageFile imageFile = new(file.Path);
            _navigationService.Navigate(CaptureToolNavigationRoutes.ImageEdit, imageFile);
        }
    }

    public void ShowCaptureOverlayOnAllMonitors()
    {
        HideMainWindow();
        CleanupCaptureOverlays();

        var captureOverlayVM = new CaptureOverlayViewModel(this);

        var monitors = MonitorCaptureHelper.CaptureAllMonitors();
        foreach (var monitor in monitors)
        {
            var window = new CaptureOverlayWindow(monitor);
            window.Closed += ImageCaptureWindow_Closed;

            captureOverlayVM.AddWindowViewModel(window.ViewModel);
            _captureOverlayWindows[monitor.HMonitor] = window;

            window.AppWindow.Show(false);

            if (window.ViewModel.IsPrimary)
            {
                // Must call SetForegroundWindow or focus will not move to the new window on activation.
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                PInvoke.SetForegroundWindow(new(hwnd));
                window.Activate();
            }
        }
    }

    public void CloseCaptureOverlays()
    {
        CleanupCaptureOverlays();
        RestoreMainWindow();
    }

    private void CleanupCaptureOverlays()
    {
        foreach (var kvp in _captureOverlayWindows)
        {
            kvp.Value.Closed -= ImageCaptureWindow_Closed;
            kvp.Value.Close();
        }
        _captureOverlayWindows.Clear();
    }

    private void ImageCaptureWindow_Closed(object sender, WindowEventArgs args)
    {
        CloseCaptureOverlays();
    }

    public void RequestCapture(MonitorCaptureResult monitor, Rectangle area)
    {
        var monitorBounds = monitor.MonitorBounds;

        // Create a bitmap for the full monitor
        using var fullBmp = new Bitmap(monitorBounds.Width, monitorBounds.Height, PixelFormat.Format32bppArgb);
        var bmpData = fullBmp.LockBits(
            new Rectangle(0, 0, monitorBounds.Width, monitorBounds.Height),
            ImageLockMode.WriteOnly,
            fullBmp.PixelFormat
        );

        try
        {
            Marshal.Copy(monitor.PixelBuffer, 0, bmpData.Scan0, monitor.PixelBuffer.Length);
        }
        finally
        {
            fullBmp.UnlockBits(bmpData);
        }

        // Crop to the selected area
        float scale = monitor.Scale;
        int cropX = (int)Math.Round((area.Left) * scale);
        int cropY = (int)Math.Round((area.Top) * scale);
        int cropWidth = (int)Math.Round(area.Width * scale);
        int cropHeight = (int)Math.Round(area.Height * scale);

        using var croppedBmp = fullBmp.Clone(new Rectangle(cropX, cropY, cropWidth, cropHeight), fullBmp.PixelFormat);
        var tempPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Temp",
            $"capture_{Guid.NewGuid()}.png"
        );

        Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
        croppedBmp.Save(tempPath, ImageFormat.Png);

        CloseCaptureOverlays();

        var imageFile = new ImageFile(tempPath);
        _navigationService.Navigate(CaptureToolNavigationRoutes.ImageEdit, imageFile);
    }

    public async Task NewVideoCaptureAsync(VideoCaptureOptions options)
    {
        // Feature check
        bool isCaptureEnabled = _featureManager.IsEnabled(CaptureToolFeatures.Feature_Capture);
        bool isVideoCaptureEnabled = _featureManager.IsEnabled(CaptureToolFeatures.Feature_Capture_Video);
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
