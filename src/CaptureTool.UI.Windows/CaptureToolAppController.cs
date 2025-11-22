using CaptureTool.Core.AppController;
using CaptureTool.Core.Navigation;
using CaptureTool.Core.Settings;
using CaptureTool.Domains.Capture.Implementations.Windows;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Interfaces.Cancellation;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Logging;
using CaptureTool.Services.Interfaces.Navigation;
using CaptureTool.Services.Interfaces.Settings;
using CaptureTool.UI.Windows.Xaml.Windows;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.Storage;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
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

    private readonly IAppNavigation _appNavigation;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localizationService;
    private readonly INavigationService _navigationService;
    private readonly ICancellationService _cancellationService;
    private readonly ISettingsService _settingsService;
    private readonly IFeatureManager _featureManager;

    private readonly SemaphoreSlim _semaphoreInit = new(1, 1);
    private readonly SemaphoreSlim _semaphoreActivation = new(1, 1);
    private readonly SemaphoreSlim _semaphoreNavigation = new(1, 1);
    private bool _isInitialized;
    private string? _tempVideoPath;
    private readonly SelectionOverlayHost _selectionOverlayHost = new();
    private readonly CaptureOverlayHost _captureOverlayHost = new();
    private readonly MainWindowHost _mainWindowHost = new();

    private UXHost _activeHost;

    public CaptureToolAppController(
        IAppNavigation appNavigation,
        ILogService logService,
        ILocalizationService localizationService,
        INavigationService navigationService,
        ICancellationService cancellationService,
        ISettingsService settingsService,
        IFeatureManager featureManager) 
    {
        _appNavigation = appNavigation;
        _logService = logService;
        _localizationService = localizationService;
        _navigationService = navigationService;
        _cancellationService = cancellationService;
        _settingsService = settingsService;
        _featureManager = featureManager;

        _navigationService.SetNavigationHandler(this);
    }

    private async Task InitializeAsync()
    {
        await _semaphoreInit.WaitAsync();

        try
        {
            if (_isInitialized)
            {
                return;
            }

            CancellationTokenSource cancellationTokenSource = _cancellationService.GetLinkedCancellationTokenSource();
            await InitializeSettingsServiceAsync(cancellationTokenSource.Token);

            bool isLoggingEnabled = _settingsService.Get(CaptureToolSettings.VerboseLogging);
            if (isLoggingEnabled)
            {
                _logService.Enable();
            }

            _localizationService.Initialize(CaptureToolSettings.Settings_LanguageOverride);

            _isInitialized = true;
        }
        finally
        {
            _semaphoreInit.Release();
        }
    }

    private void OnSelectionOverlayHostLostFocus(object? sender, EventArgs e)
    {
        if (_appNavigation.CanGoBack)
        {
            _appNavigation.GoBackToMainWindow();
        }
        else
        {
            Shutdown();
        }
    }

    #region INavigationHandler
    public async void HandleNavigationRequest(INavigationRequest request)
    {
        await _semaphoreNavigation.WaitAsync();

        try
        {
            if (CaptureToolNavigationRouteHelper.IsMainWindowRoute(request.Route))
            {
                switch (_activeHost)
                {
                    case UXHost.MainWindow:
                        break;

                    case UXHost.SelectionOverlay:
                        _selectionOverlayHost.LostFocus -= OnSelectionOverlayHostLostFocus;
                        _mainWindowHost.ExcludeWindowFromCapture(false);
                        _selectionOverlayHost.Close();
                        break;

                    case UXHost.CaptureOverlay:
                        _mainWindowHost.ExcludeWindowFromCapture(false);
                        _captureOverlayHost.Close();
                        break;
                }

                _mainWindowHost.Show();
                _mainWindowHost.HandleNavigationRequest(request);
                _activeHost = UXHost.MainWindow;
            }
            else if (request.Route is CaptureToolNavigationRoute imageRoute && imageRoute == CaptureToolNavigationRoute.ImageCapture)
            {
                if (request.Parameter is not CaptureOptions options)
                {
                    throw new InvalidOperationException("Image capture cannot be started without options.");
                }

                switch (_activeHost)
                {
                    case UXHost.MainWindow:
                        _mainWindowHost.ExcludeWindowFromCapture(true);
                        _mainWindowHost.Hide();
                        await Task.Delay(200);
                        break;

                    case UXHost.SelectionOverlay:
                        return;

                    case UXHost.CaptureOverlay:
                        _captureOverlayHost.Close();
                        break;
                }

                _selectionOverlayHost.LostFocus += OnSelectionOverlayHostLostFocus;
                _selectionOverlayHost.Initialize(options);
                _selectionOverlayHost.Activate();
                _activeHost = UXHost.SelectionOverlay;
            }
            else if (request.Route is CaptureToolNavigationRoute videoRoute && videoRoute == CaptureToolNavigationRoute.VideoCapture)
            {
                if (request.Parameter is not NewCaptureArgs args)
                {
                    throw new InvalidOperationException("Video capture cannot be started without arguments.");
                }

                switch (_activeHost)
                {
                    case UXHost.MainWindow:
                        _mainWindowHost.ExcludeWindowFromCapture(true);
                        _mainWindowHost.Hide();
                        await Task.Delay(200);
                        break;

                    case UXHost.SelectionOverlay:
                        _selectionOverlayHost.LostFocus -= OnSelectionOverlayHostLostFocus;
                        _selectionOverlayHost.Close();
                        break;

                    case UXHost.CaptureOverlay:
                        return;
                }

                _captureOverlayHost.Initialize(args);
                _captureOverlayHost.Activate();
                _activeHost = UXHost.CaptureOverlay;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(request), $"No handler found for route: {request.Route}");
            }
        }
        finally
        {
            _semaphoreNavigation.Release();
        }
    }
    #endregion

    #region IActivationHandler
    public async Task HandleLaunchActivationAsync()
    {
        await _semaphoreActivation.WaitAsync();

        try
        {
            await InitializeAsync();
            _appNavigation.GoHome();
        }
        finally
        {
            _semaphoreActivation.Release();
        }
    }

    public async Task HandleProtocolActivationAsync(Uri protocolUri)
    {
        await _semaphoreActivation.WaitAsync();

        try 
        {
            if (!protocolUri.Scheme.Equals("ms-screenclip", StringComparison.InvariantCultureIgnoreCase))
            {
                return;
            }

            await InitializeAsync();

            NameValueCollection queryParams = HttpUtility.ParseQueryString(protocolUri.Query) ?? [];
            bool isRecordingType = queryParams.Get("type") is string type && type.Equals("recording", StringComparison.InvariantCultureIgnoreCase);

            string source = queryParams.Get("source") ?? string.Empty;
            if (source.Equals("PrintScreen", StringComparison.InvariantCultureIgnoreCase))
            {
                _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
            }
            else if (source.Equals("ScreenRecorderHotKey", StringComparison.InvariantCultureIgnoreCase) || isRecordingType)
            {
                if (_featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture))
                {
                    _appNavigation.GoToImageCapture(CaptureOptions.VideoDefault);
                }
                else
                {
                    _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
                }
            }
            else if (source.Equals("HotKey", StringComparison.InvariantCultureIgnoreCase))
            {
                _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
            }
            else
            {
                _appNavigation.GoHome();
            }
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to handle protocol activation.");
        }
        finally
        {
            _semaphoreActivation.Release();
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
        _appNavigation.GoToVideoCapture(args);
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
        _appNavigation.GoToVideoEdit(videoFile);
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
            global::Windows.Storage.StorageFile file = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(imageFile.FilePath);

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

            string tempFilePath = imageFile.FilePath;
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
