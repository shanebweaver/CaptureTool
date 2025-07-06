using CaptureTool.Capture;
using CaptureTool.Capture.Windows;
using CaptureTool.Common.Storage;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;
using CaptureTool.UI.Windows.Xaml.Extensions;
using CaptureTool.UI.Windows.Xaml.Windows;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.Storage;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Win32;
using WinRT.Interop;

namespace CaptureTool.UI.Windows;

internal partial class CaptureToolAppController : IAppController
{
    private readonly ILogService _logService;
    private readonly INavigationService _navigationService;
    
    private readonly HashSet<IntPtr> _captureOverlayWindowHandles = [];
    private CaptureOverlayViewModel? _captureOverlayViewModel;

    public CaptureToolAppController(
        ILogService logService,
        INavigationService navigationService) 
    {
        _logService = logService;
        _navigationService = navigationService;
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

    public async void ShowCaptureOverlay()
    {
        // Give the window time to close so it isn't included in the capture
        CloseCaptureOverlay();
        HideMainWindow();
        await Task.Delay(200);

        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            _captureOverlayWindowHandles.Clear();
            _captureOverlayViewModel = new(this);

            Window? primaryWindow = null;
            var monitors = MonitorCaptureHelper.CaptureAllMonitors();
            foreach (var monitor in monitors)
            {
                CaptureOverlayWindow window = new(monitor);

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

    private DispatcherTimer? _foregroundTimer;

    private void StartForegroundMonitor()
    {
        if (_foregroundTimer == null)
        {
            _foregroundTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };

            _foregroundTimer.Tick += (_, _) =>
            {
                IntPtr foregroundHwnd = PInvoke.GetForegroundWindow();
                if (!_captureOverlayWindowHandles.Contains(foregroundHwnd))
                {
                    ShowMainWindow();
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
            ApplicationData.GetDefault().TemporaryPath,
            "screenshots",
            $"capture_{Guid.NewGuid()}.png"
        );

        Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
        croppedBmp.Save(tempPath, ImageFormat.Png);

        CloseCaptureOverlay();
        RestoreMainWindow();

        var imageFile = new ImageFile(tempPath);
        _navigationService.Navigate(CaptureToolNavigationRoutes.ImageEdit, imageFile);
    }

    public nint GetMainWindowHandle()
    {
        return WindowNative.GetWindowHandle(App.Current.MainWindow);
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
            App.Current.MainWindow?.AppWindow.Hide();
        });
    }

    public void ShowMainWindow()
    {
        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            App.Current.MainWindow?.AppWindow.Show(false);
        });
    }

    public void CloseCaptureOverlay()
    {
        StopForegroundTimer();

        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            _captureOverlayWindowHandles.Clear();
            _captureOverlayViewModel?.Close();
            _captureOverlayViewModel = null;
        });
    }

    private static void RestoreMainWindow()
    {
        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            if (App.Current.MainWindow != null)
            {
                App.Current.MainWindow.Restore();
                App.Current.MainWindow.Activate();

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
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
}
