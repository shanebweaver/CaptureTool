using CaptureTool.Capture;
using CaptureTool.Capture.Windows;
using CaptureTool.Common.Storage;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Settings;
using CaptureTool.UI.Windows.Xaml.Windows;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.Storage;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CaptureTool.UI.Windows;

internal partial class CaptureToolAppController : IAppController
{
    private readonly ILogService _logService;
    private readonly INavigationService _navigationService;
    private readonly ICancellationService _cancellationService;
    private readonly ISettingsService _settingsService;
    private readonly IFeatureManager _featureManager;

    private CaptureOverlayHost? _overlayHost;
    private MainWindowHost? _mainWindowHost;

    private readonly SemaphoreSlim _semaphore = new(1,1);
    private bool _isInitialized;

    public CaptureToolAppController(
        ILogService logService,
        INavigationService navigationService,
        ICancellationService cancellationService,
        ISettingsService settingsService,
        IFeatureManager featureManager) 
    {
        _logService = logService;
        _navigationService = navigationService;
        _cancellationService = cancellationService;
        _settingsService = settingsService;
        _featureManager = featureManager;
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
                ShowCaptureOverlay(CaptureOptions.ImageDefault);
            }
            else if (source == "ScreenRecorderHotKey" || isRecordingType)
            {
                if (_featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture))
                {
                    ShowCaptureOverlay(CaptureOptions.VideoDefault);
                }
                else
                {
                    ShowCaptureOverlay(CaptureOptions.ImageDefault);
                }
            }
            else if (source == "HotKey")
            {
                ShowCaptureOverlay(CaptureOptions.ImageDefault);
            }
            else
            {
                RestoreMainWindow();
            }
        }
    }

    public bool TryRestart()
    {
        global::Windows.ApplicationModel.Core.AppRestartFailureReason restartError = AppInstance.Restart(string.Empty);

        switch (restartError)
        {
            case global::Windows.ApplicationModel.Core.AppRestartFailureReason.NotInForeground:
                _logService.LogWarning("The app is not in the foreground.");
                break;
            case global::Windows.ApplicationModel.Core.AppRestartFailureReason.RestartPending:
                _logService.LogWarning("Another restart is currently pending.");
                break;
            case global::Windows.ApplicationModel.Core.AppRestartFailureReason.InvalidUser:
                _logService.LogWarning("Current user is not signed in or not a valid user.");
                break;
            case global::Windows.ApplicationModel.Core.AppRestartFailureReason.Other:
                _logService.LogWarning("Failure restarting.");
                break;
        }

        return false;
    }

    public async void ShowCaptureOverlay(CaptureOptions? options = null)
    {
        ThrowIfNotInitialized();

        CloseCaptureOverlay();
        HideMainWindow();
        await Task.Delay(200);

        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            _overlayHost = new CaptureOverlayHost(() =>
            {
                ShowMainWindow(false);
                CloseCaptureOverlay();
            });

            _overlayHost.Show(options ?? new(CaptureMode.Image, CaptureType.Rectangle));
        });
    }

    public void CloseCaptureOverlay()
    {
        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            _overlayHost?.Dispose();
            _overlayHost = null;
        });
    }

    public void PerformAllScreensCapture()
    {
        ThrowIfNotInitialized();

        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            MonitorCaptureResult[] monitors = _overlayHost?.GetMonitors() ?? MonitorCaptureHelper.CaptureAllMonitors();
            Bitmap? combined = MonitorCaptureHelper.CombineMonitors(monitors);
            if (combined != null)
            {
                try
                {
                    var tempPath = Path.Combine(
                        ApplicationData.GetDefault().TemporaryPath,
                        $"capture_{Guid.NewGuid()}.png"
                    );
                    combined.Save(tempPath, ImageFormat.Png);

                    RestoreMainWindow();
                    CloseCaptureOverlay();

                    var imageFile = new ImageFile(tempPath);
                    _navigationService.Navigate(CaptureToolNavigationRoutes.ImageEdit, imageFile, true);
                    TryAutoSaveImage(imageFile);
                    _ = TryAutoCopyImageAsync(imageFile);
                }
                finally
                {
                    combined.Dispose();
                }
            }
        });
    }

    public void PerformImageCapture(MonitorCaptureResult monitor, Rectangle area)
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
        _navigationService.Navigate(CaptureToolNavigationRoutes.ImageEdit, imageFile, true);
        TryAutoSaveImage(imageFile);
        _ = TryAutoCopyImageAsync(imageFile);
    }

    public void PrepareForVideoCapture(MonitorCaptureResult monitor, Rectangle area)
    {
        Trace.Assert(_featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture));

        if (_overlayHost == null)
        {
            ShowCaptureOverlay();
        }
        _overlayHost?.TransitionToVideoMode(monitor, area);
    }

    public async void StartVideoCapture(MonitorCaptureResult monitor, Rectangle area)
    {
        if (_overlayHost == null)
        {
            ShowCaptureOverlay();
            _overlayHost?.TransitionToVideoMode(monitor, area);
        }

        Debug.WriteLine("Preparing...");

        var screenRecorder = new ScreenRecorder(monitor.HMonitor, area, Path.Join(GetDefaultScreenshotsFolderPath(), "test.mp4"));

        Debug.WriteLine("ScreenRecorder started");

        screenRecorder.StartRecording();

        Debug.WriteLine("Recording...");

        await Task.Delay(5000);

        Debug.WriteLine("Recorded 5 seconds");

        screenRecorder.StopRecording();

        Debug.WriteLine("ScreenRecorder stopped");

        screenRecorder.Dispose();
        Debug.WriteLine("ScreenRecorder disposed");

    }

    public nint GetMainWindowHandle()
    {
        return _mainWindowHost?.Handle ?? IntPtr.Zero;
    }

    public string GetDefaultScreenshotsFolderPath()
    {
        return global::Windows.Storage.KnownFolders.SavedPictures.Path;
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
        _mainWindowHost?.Hide();
    }

    public void ShowMainWindow(bool activate = true)
    {
        _mainWindowHost?.Show(activate);
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

    public void Shutdown()
    {
        lock (this)
        {
            try
            {
                CloseCaptureOverlay();
                CleanupMainWindow();
                _cancellationService.CancelAll();
            }
            catch (Exception e)
            {
                Debug.Fail($"Error during shutdown: {e.Message}");
            }

            Environment.Exit(0);
        }
    }

    private void CheckExit()
    {
        if (_mainWindowHost == null)
        {
            Shutdown();
        }
    }

    private void RestoreMainWindow()
    {
        _mainWindowHost ??= new MainWindowHost(OnMainWindowClosed);
        _mainWindowHost.Restore();
    }

    private void OnMainWindowClosed()
    {
        CleanupMainWindow();
        CheckExit();
    }

    private void CleanupMainWindow()
    {
        _mainWindowHost?.Dispose();
        _mainWindowHost = null;
    }

    private async Task InitializeSettingsServiceAsync(CancellationToken cancellationToken)
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string settingsFilePath = Path.Combine(appDataPath, "Settings.json");
        await _settingsService.InitializeAsync(settingsFilePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void ThrowIfNotInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException($"{nameof(CaptureToolAppController)} must be initialized before it can be used.");
        }
    }

    private async Task<bool> TryAutoCopyImageAsync(ImageFile imageFile)
    {
        try
        {
            bool autoCopy = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy);
            if (!autoCopy)
            {
                return false;
            }

            // Load the file
            global::Windows.Storage.StorageFile file = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(imageFile.Path);

            // Open the file as a stream
            using IRandomAccessStream stream = await file.OpenAsync(global::Windows.Storage.FileAccessMode.Read);

            // Decode the bitmap
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            // Convert to a compatible format (Clipboard requires BGRA8 with premultiplied alpha)
            SoftwareBitmap converted = SoftwareBitmap.Convert(
                softwareBitmap,
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied
            );

            // Encode to PNG into a stream
            InMemoryRandomAccessStream inMemoryStream = new();
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, inMemoryStream);
            encoder.SetSoftwareBitmap(converted);
            await encoder.FlushAsync();

            // Prepare clipboard content
            DataPackage dataPackage = new();
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(inMemoryStream));
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();

            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryAutoSaveImage(ImageFile imageFile)
    {
        try
        {
            bool autoSave = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave);
            if (!autoSave)
            {
                return false;
            }

            string screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_ScreenshotsFolder);
            if (string.IsNullOrWhiteSpace(screenshotsFolder))
            {
                screenshotsFolder = GetDefaultScreenshotsFolderPath();
            }

            string tempFilePath = imageFile.Path;
            string fileName = Path.GetFileName(tempFilePath);
            string newFilePath = Path.Combine(screenshotsFolder, fileName);

            File.Copy(tempFilePath, newFilePath, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

}
