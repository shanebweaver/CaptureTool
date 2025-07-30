using CaptureTool.Capture;
using CaptureTool.Capture.Windows;
using CaptureTool.Common.Storage;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;
using CaptureTool.UI.Windows.Xaml.Extensions;
using CaptureTool.UI.Windows.Xaml.Windows;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.Storage;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Windows.ApplicationModel.Core;
using Windows.Win32;
using WinRT.Interop;

namespace CaptureTool.UI.Windows;

internal partial class CaptureToolAppController : IAppController
{
    private readonly ILogService _logService;
    private readonly INavigationService _navigationService;
    private readonly ICancellationService _cancellationService;
    private readonly ISettingsService _settingsService;

    private readonly HashSet<CaptureOverlayWindow> _captureOverlayWindows = [];
    private readonly HashSet<IntPtr> _captureOverlayWindowHandles = [];
    private CaptureOverlayViewModel? _captureOverlayViewModel;
    private MainWindow? _mainWindow;
    private DispatcherTimer? _foregroundTimer;
    private readonly SemaphoreSlim _semaphore = new(1,1);
    private bool _isInitialized;

    public CaptureToolAppController(
        ILogService logService,
        INavigationService navigationService,
        ICancellationService cancellationService,
        ISettingsService settingsService) 
    {
        _logService = logService;
        _navigationService = navigationService;
        _cancellationService = cancellationService;
        _settingsService = settingsService;
    }

    public async Task InitializeAsync()
    {
        await _semaphore.WaitAsync();

        try
        {
            if (_isInitialized)
            {
                return;
            }

            CancellationTokenSource cancellationTokenSource = _cancellationService.GetLinkedCancellationTokenSource();
            await InitializeSettingsServiceAsync(cancellationTokenSource.Token);

            _isInitialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void ThrowIfNotInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException($"{nameof(CaptureToolAppController)} must be initialized before it can be used.");
        }
    }

    public async Task HandleLaunchActicationAsync()
    {
        await InitializeAsync();
        RestoreMainWindow();
        GoHome();
    }

    public async Task HandleProtocolActivationAsync(Uri protocolUri)
    {
        if (protocolUri.Scheme == "ms-screenclip")
        {
            await InitializeAsync();

            NameValueCollection queryParams = HttpUtility.ParseQueryString(protocolUri.Query) ?? [];
            bool isRecordingType = queryParams.Get("type") is string type && type == "recording";

            string source = queryParams.Get("source") ?? string.Empty;
            if (source == "PrintScreen")
            {
                // PrtSc key modern behavior.
                // Windows 11 has a global setting and will only call ms-screenclip if the user want to show the capture app.
                // In Windows Settings > Accessibility > Keyboard, "Use the Print screen key to open screen capture"
                CaptureMode captureMode = CaptureMode.Image;
                CaptureType captureType = CaptureType.Rectangle;
                CaptureOptions captureOptions = new(captureMode, captureType);
                ShowCaptureOverlay(captureOptions);

                // Legacy behavior
                // Capture all monitors silently and put the image in the user's clipboard.
                //List<MonitorCaptureResult> monitors = MonitorCaptureHelper.CaptureAllMonitors();
                //ClipboardImageHelper.CombineMonitorsAndCopyToClipboard(monitors);
            }
            else if (source == "ScreenRecorderHotKey" || isRecordingType)
            {
                // Video capture
                CaptureMode captureMode = CaptureMode.Video;
                CaptureType captureType = CaptureType.Rectangle;
                CaptureOptions captureOptions = new(captureMode, captureType);
                ShowCaptureOverlay(captureOptions);
            }
            else if (source == "HotKey")
            {
                // Image capture
                CaptureMode captureMode = CaptureMode.Image;
                CaptureType captureType = CaptureType.Rectangle;
                CaptureOptions captureOptions = new(captureMode, captureType);
                ShowCaptureOverlay(captureOptions);
            }
        }
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

    public async void ShowCaptureOverlay(CaptureOptions? options = null)
    {
        ThrowIfNotInitialized();

        // Give the window time to close so it isn't included in the capture
        CloseCaptureOverlay();
        HideMainWindow();
        await Task.Delay(200);

        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            _captureOverlayWindowHandles.Clear();
            _captureOverlayViewModel = new();

            //var allWindows = WindowInfoHelper.GetAllWindows();
            var monitors = MonitorCaptureHelper.CaptureAllMonitors();

            Window? primaryWindow = null;
            foreach (var monitor in monitors)
            {
                // Uncomment to only show overlay on primary monitor.
                // Useful for debugging on a side monitor.
                if (!monitor.IsPrimary)
                {
                    continue;
                }

                //var monitorWindows = allWindows.Select((wi) => {
                //    var position = wi.Position;
                //    var scale = monitor.Scale;
                //    var scaled = new Rectangle(
                //        (int)(position.X * scale),
                //        (int)(position.Y * scale),
                //        (int)(position.Width * scale),
                //        (int)(position.Height * scale));
                //    return scaled;
                //}).Where(p => monitor.MonitorBounds.IntersectsWith(p) || monitor.MonitorBounds.Contains(p));

                CaptureOverlayWindow window = new(monitor, []);
                _captureOverlayWindows.Add(window);

                IntPtr windowHwnd = WindowNative.GetWindowHandle(window);
                _captureOverlayWindowHandles.Add(windowHwnd);

                _captureOverlayViewModel.AddWindowViewModel(window.ViewModel);
                if (window.ViewModel.IsPrimary)
                {
                    primaryWindow = window;
                    primaryWindow.Activated += PrimaryWindow_Activated;
                }
            }

            // Activate the primary window so the UI gets focus.
            primaryWindow?.Activate();
        });
    }

    private void PrimaryWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (sender is Window window)
        {
            window.Activated -= PrimaryWindow_Activated;
            StartForegroundMonitor();
        }
    }

    private void StartForegroundMonitor()
    {
        if (_foregroundTimer == null)
        {
            _foregroundTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };

            _foregroundTimer.Tick += (_, _) =>
            {
                IntPtr foregroundHwnd = PInvoke.GetForegroundWindow();
                if (!_captureOverlayWindowHandles.Contains(foregroundHwnd))
                {
                    ShowMainWindow(false);
                    CloseCaptureOverlay();
                }
            };
        }

        _foregroundTimer.Start();
    }

    private void StopForegroundTimer()
    {
        _foregroundTimer?.Stop();
        _foregroundTimer = null;
    }

    public void PerformCapture(MonitorCaptureResult monitor, Rectangle area)
    {
        ThrowIfNotInitialized();

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

        var tempPath = Path.Combine(
            ApplicationData.GetDefault().TemporaryPath,
            $"capture_{Guid.NewGuid()}.png"
        );

        // Crop to the selected area
        float scale = monitor.Scale; int cropX = (int)Math.Round((area.Left) * scale);
        int cropY = (int)Math.Round((area.Top) * scale);
        int cropWidth = (int)Math.Round(area.Width * scale);
        int cropHeight = (int)Math.Round(area.Height * scale);

        // Ensure cropping stays within image bounds
        cropX = Math.Clamp(cropX, 0, fullBmp.Width - 1);
        cropY = Math.Clamp(cropY, 0, fullBmp.Height - 1);
        cropWidth = Math.Clamp(cropWidth, 1, fullBmp.Width - cropX);
        cropHeight = Math.Clamp(cropHeight, 1, fullBmp.Height - cropY);

        var cropRect = new Rectangle(cropX, cropY, cropWidth, cropHeight);
        using var croppedBmp = fullBmp.Clone(cropRect, fullBmp.PixelFormat);
        croppedBmp.Save(tempPath, ImageFormat.Png);

        RestoreMainWindow();
        CloseCaptureOverlay();

        var imageFile = new ImageFile(tempPath);
        _navigationService.Navigate(CaptureToolNavigationRoutes.ImageEdit, imageFile);
    }

    public nint GetMainWindowHandle()
    {
        return WindowNative.GetWindowHandle(_mainWindow);
    }

    public void GoHome()
    {
        if (_navigationService.CurrentRoute != CaptureToolNavigationRoutes.Home)
        {
            _navigationService.Navigate(CaptureToolNavigationRoutes.Home, clearHistory: true);
        }
    }

    public void HideMainWindow()
    {
        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            _mainWindow?.AppWindow.Hide();
        });
    }

    public void ShowMainWindow(bool activate = true)
    {
        App.Current.DispatcherQueue.TryEnqueue(() =>
        {            
            _mainWindow?.AppWindow.Show(activate);

            if (activate)
            {
                var hwnd = WindowNative.GetWindowHandle(_mainWindow);
                PInvoke.SetForegroundWindow(new(hwnd));
            }
        });
    }

    public void CloseCaptureOverlay()
    {
        StopForegroundTimer();

        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            _captureOverlayWindowHandles.Clear();

            _captureOverlayViewModel?.Unload();
            _captureOverlayViewModel = null;

            foreach (var window in _captureOverlayWindows)
            {
                window.Close();
            }
            _captureOverlayWindows.Clear();
        });
    }

    private void RestoreMainWindow()
    {
        if (_mainWindow == null)
        {
            _mainWindow = new MainWindow();
            _mainWindow.Closed += OnWindowClosed;
        }

        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            if (_mainWindow != null)
            {
                _mainWindow.Restore();
                _mainWindow.Activate();

                var hwnd = WindowNative.GetWindowHandle(_mainWindow);
                PInvoke.SetForegroundWindow(new(hwnd));
            }
        });
    }

    public bool TryGoBack()
    {
        if (_navigationService.CanGoBack)
        {
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

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        CleanupWindow();
        CheckExit();
    }

    private void CheckExit()
    {
        if (_mainWindow == null)
        {
            Shutdown();
        }
    }

    private void CleanupWindow()
    {
        if (_mainWindow != null)
        {
            _mainWindow.Closed -= OnWindowClosed;
            _mainWindow.Close();
            _mainWindow = null;
        }
    }

    public void Shutdown()
    {
        lock (this)
        {
            try
            {
                CleanupWindow();
                _cancellationService.CancelAll();
            }
            catch (Exception e)
            {
                Debug.Fail($"Error during shutdown: {e.Message}");
            }

            Application.Current.Exit();
        }
    }

    private async Task InitializeSettingsServiceAsync(CancellationToken cancellationToken)
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string settingsFilePath = Path.Combine(appDataPath, "Settings.json");
        await _settingsService.InitializeAsync(settingsFilePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
