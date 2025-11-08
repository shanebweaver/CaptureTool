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
    private enum UXHost
    {
        None,
        MainWindow,
        SelectionOverlay,
        CaptureOverlay
    }

    private readonly ILogService _logService;
    private readonly INavigationService _navigationService;
    private readonly ICancellationService _cancellationService;
    private readonly ISettingsService _settingsService;
    private readonly IFeatureManager _featureManager;

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _isInitialized;
    private string? _tempVideoPath;
    private readonly SelectionOverlayHost _selectionOverlayHost = new();
    private readonly CaptureOverlayHost _captureOverlayHost = new();
    private readonly MainWindowHost _mainWindowHost = new();

    private UXHost _activeHost;

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

        _navigationService.SetNavigationHandler(this);

        _selectionOverlayHost.LostFocus += (s, e) =>
        {
            GoHome();
        };
    }

    private async Task InitializeAsync()
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

    #region INavigationHandler
    public void HandleNavigationRequest(NavigationRequest request)
    {
        if (request.Route == CaptureToolNavigationRoutes.Home ||
            request.Route == CaptureToolNavigationRoutes.Loading ||
            request.Route == CaptureToolNavigationRoutes.AddOns ||
            request.Route == CaptureToolNavigationRoutes.Error ||
            request.Route == CaptureToolNavigationRoutes.About ||
            request.Route == CaptureToolNavigationRoutes.Settings ||
            request.Route == CaptureToolNavigationRoutes.ImageEdit ||
            request.Route == CaptureToolNavigationRoutes.VideoEdit)
        {
            switch (_activeHost)
            {
                case UXHost.MainWindow:
                    break;

                case UXHost.SelectionOverlay:
                    _selectionOverlayHost.Close();
                    break;

                case UXHost.CaptureOverlay:
                    _captureOverlayHost.Close();
                    break;
            }

            _mainWindowHost.HandleNavigationRequest(request);
            _activeHost = UXHost.MainWindow;
        }
        else if (
            request.Route == CaptureToolNavigationRoutes.ImageCapture &&
            request.Parameter is CaptureOptions options)
        {
            switch (_activeHost)
            {
                case UXHost.MainWindow:
                    _mainWindowHost.Hide();
                    break;

                case UXHost.SelectionOverlay:
                    return;

                case UXHost.CaptureOverlay:
                    _captureOverlayHost.Close();
                    break;
            }

            _selectionOverlayHost.Show(options);
            _activeHost = UXHost.SelectionOverlay;
        }
        else if (
            request.Route == CaptureToolNavigationRoutes.VideoCapture &&
            request.Parameter is NewCaptureArgs args)
        {
            switch (_activeHost)
            {
                case UXHost.MainWindow:
                    _mainWindowHost.Hide();
                    break;

                case UXHost.SelectionOverlay:
                    _selectionOverlayHost.Close();
                    break;

                case UXHost.CaptureOverlay:
                    return;
            }

            _captureOverlayHost.Show(args);
            _activeHost = UXHost.CaptureOverlay;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }
    }
    #endregion

    #region IActivationHandler
    public async Task HandleLaunchActivationAsync()
    {
        await InitializeAsync();
        GoHome();
    }

    public async Task HandleProtocolActivationAsync(Uri protocolUri)
    {
        if (protocolUri.Scheme.Equals("ms-screenclip", StringComparison.InvariantCultureIgnoreCase))
        {
            await InitializeAsync();

            NameValueCollection queryParams = HttpUtility.ParseQueryString(protocolUri.Query) ?? [];
            bool isRecordingType = queryParams.Get("type") is string type && type.Equals("recording", StringComparison.InvariantCultureIgnoreCase);

            string source = queryParams.Get("source") ?? string.Empty;
            if (source.Equals("PrintScreen", StringComparison.InvariantCultureIgnoreCase))
            {
                _navigationService.Navigate(CaptureToolNavigationRoutes.ImageCapture, CaptureOptions.ImageDefault);
            }
            else if (source.Equals("ScreenRecorderHotKey", StringComparison.InvariantCultureIgnoreCase) || isRecordingType)
            {
                if (_featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture))
                {
                    _navigationService.Navigate(CaptureToolNavigationRoutes.ImageCapture, CaptureOptions.VideoDefault);
                }
                else
                {
                    _navigationService.Navigate(CaptureToolNavigationRoutes.ImageCapture, CaptureOptions.ImageDefault);
                }
            }
            else if (source.Equals("HotKey", StringComparison.InvariantCultureIgnoreCase))
            {
                _navigationService.Navigate(CaptureToolNavigationRoutes.ImageCapture, CaptureOptions.ImageDefault);
            }
            else
            {
                GoHome();
            }
        }
    }
    #endregion

    #region IImageCaptureHandler
    public ImageFile PerformAllScreensCapture()
    {
        ThrowIfNotInitialized();

        MonitorCaptureResult[] monitors = _selectionOverlayHost?.GetMonitors() ?? MonitorCaptureHelper.CaptureAllMonitors();
        Bitmap combined = MonitorCaptureHelper.CombineMonitors(monitors);
        try
        {
            var tempPath = Path.Combine(
                ApplicationData.GetDefault().TemporaryPath,
                $"capture_{Guid.NewGuid()}.png"
            );
            combined.Save(tempPath, ImageFormat.Png);

            ImageFile imageFile = new(tempPath);
            TryAutoSaveImage(imageFile);
            _ = TryAutoCopyImageAsync(imageFile);

            return imageFile;
        }
        finally
        {
            combined.Dispose();
        }
    }

    public ImageFile PerformImageCapture(NewCaptureArgs args)
    {
        ThrowIfNotInitialized();

        var monitor = args.Monitor;
        var area = args.Area;
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

        var imageFile = new ImageFile(tempPath);
        TryAutoSaveImage(imageFile);
        _ = TryAutoCopyImageAsync(imageFile);

        return imageFile;
    }
    #endregion

    #region IVideoCaptureHandler
    public void StartVideoCapture(NewCaptureArgs args)
    {
        _navigationService.Navigate(CaptureToolNavigationRoutes.VideoCapture, args);
        _captureOverlayHost?.HideBorder();

        _tempVideoPath = Path.Combine(
            ApplicationData.GetDefault().TemporaryPath,
            $"capture_{Guid.NewGuid()}.mp4"
        );

        ScreenRecorder.StartRecording(args.Monitor.HMonitor, _tempVideoPath);
    }

    public VideoFile StopVideoCapture()
    {
        if (string.IsNullOrEmpty(_tempVideoPath))
        {
            throw new InvalidOperationException("Cannot stop, no video is recording.");
        }

        ScreenRecorder.StopRecording();

        VideoFile videoFile = new(_tempVideoPath);
        _navigationService.Navigate(CaptureToolNavigationRoutes.VideoEdit, videoFile);
        _tempVideoPath = null;

        return videoFile;
    }

    public void CancelVideoCapture()
    {
        ScreenRecorder.StopRecording();
        _tempVideoPath = null;
    }
    #endregion

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

    public void Shutdown()
    {
        lock (this)
        {
            try
            {
                _captureOverlayHost.Dispose();
                _selectionOverlayHost.Dispose();
                _mainWindowHost.Dispose();
                _cancellationService.CancelAll();
            }
            catch (Exception e)
            {
                Debug.Fail($"Error during shutdown: {e.Message}");
            }

            Environment.Exit(0);
        }
    }

    public nint GetMainWindowHandle()
    {
        return _mainWindowHost.Handle;
    }

    public string GetDefaultScreenshotsFolderPath()
    {
        return global::Windows.Storage.KnownFolders.SavedPictures.Path;
    }

    private void GoHome()
    {
        _navigationService.Navigate(CaptureToolNavigationRoutes.Home, clearHistory: true);
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
