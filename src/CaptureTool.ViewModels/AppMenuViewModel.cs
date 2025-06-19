using CaptureTool.Capture.Image;
using CaptureTool.Capture.Video;
using CaptureTool.Common.Commands;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Telemetry;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CaptureTool.ViewModels;

public sealed partial class AppMenuViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "AppMenuViewModel_Load";
        public static readonly string Unload = "AppMenuViewModel_Unload";
        public static readonly string NewImageCapture = "AppMenuViewModel_NewImageCapture";
        public static readonly string NewVideoCapture = "AppMenuViewModel_NewVideoCapture";
        public static readonly string NewAudioCapture = "AppMenuViewModel_NewAudioCapture";
        public static readonly string OpenFile = "AppMenuViewModel_OpenFile";
        public static readonly string NavigateToSettings = "AppMenuViewModel_NavigateToSettings";
        public static readonly string ShowAboutApp = "AppMenuViewModel_ShowAboutApp";
        public static readonly string ExitApplication = "AppMenuViewModel_ExitApplication";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly ICancellationService _cancellationService;
    private readonly INavigationService _navigationService;
    private readonly IAppController _appController;
    private readonly IFeatureManager _featureManager;

    public RelayCommand NewImageCaptureCommand => new(NewImageCapture, () => IsImageCaptureEnabled);
    public RelayCommand NewVideoCaptureCommand => new(NewVideoCapture, () => IsVideoCaptureEnabled);
    public RelayCommand NewAudioCaptureCommand => new(NewAudioCapture, () => IsAudioCaptureEnabled);
    public RelayCommand OpenFileCommand => new(OpenFile);
    public RelayCommand NavigateToSettingsCommand => new(NavigateToSettings);
    public RelayCommand ShowAboutAppCommand => new(ShowAboutApp);
    public RelayCommand ExitApplicationCommand => new(ExitApplication);

    private bool _isImageCaptureEnabled;
    public bool IsImageCaptureEnabled
    {
        get => _isImageCaptureEnabled;
        set => Set(ref _isImageCaptureEnabled, value);
    }

    private bool _isVideoCaptureEnabled;
    public bool IsVideoCaptureEnabled
    {
        get => _isVideoCaptureEnabled;
        set => Set(ref _isVideoCaptureEnabled, value);
    }

    private bool _isAudioCaptureEnabled;
    public bool IsAudioCaptureEnabled
    {
        get => _isAudioCaptureEnabled;
        set => Set(ref _isAudioCaptureEnabled, value);
    }

    public AppMenuViewModel(
        ITelemetryService telemetryService,
        ICancellationService cancellationService,
        IAppController appController,
        INavigationService navigationService,
        IFeatureManager featureManager)
    {
        _telemetryService = telemetryService;
        _cancellationService = cancellationService;
        _appController = appController;
        _navigationService = navigationService;
        _featureManager = featureManager;
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Debug.Assert(IsUnloaded);
        StartLoading();

        string activityId = ActivityIds.Load;
        _telemetryService.ActivityInitiated(activityId);

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            IsImageCaptureEnabled = _featureManager.IsEnabled(CaptureToolFeatures.Feature_Capture_Image);
            IsVideoCaptureEnabled = _featureManager.IsEnabled(CaptureToolFeatures.Feature_Capture_Video);
            IsAudioCaptureEnabled = _featureManager.IsEnabled(CaptureToolFeatures.Feature_Capture_Audio);
        
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (OperationCanceledException)
        {
            _telemetryService.ActivityCanceled(activityId);
            throw;
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
            throw;
        }
        finally
        {
            cts.Dispose();
        }

        await base.LoadAsync(parameter, cancellationToken);
    }

    override public void Unload()
    {
        string activityId = ActivityIds.Unload;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            IsAudioCaptureEnabled = false;
            IsImageCaptureEnabled = false;
            IsVideoCaptureEnabled = false;

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }

        base.Unload();
    }

    private void NewImageCapture()
    {
        string activityId = ActivityIds.NewImageCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            ImageCaptureOptions options = new(ImageCaptureMode.Rectangle, ImageFileType.Png, true);
            _ = _appController.NewImageCaptureAsync(options);
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void NewVideoCapture()
    {
        string activityId = ActivityIds.NewVideoCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            VideoCaptureOptions options = new(VideoCaptureMode.Rectangle, VideoFileType.Mp4, true);
            _ = _appController.NewVideoCaptureAsync(options);
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void NewAudioCapture()
    {
        string activityId = ActivityIds.NewAudioCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _ = _appController.NewAudioCaptureAsync(); 
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private async void OpenFile()
    {
        string activityId = ActivityIds.OpenFile;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            var filePicker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };
            filePicker.FileTypeFilter.Add(".png");

            nint hwnd = _appController.GetMainWindowHandle();
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

            StorageFile file = await filePicker.PickSingleFileAsync();
            if (file == null)
            {
                _telemetryService.ActivityCanceled(activityId);
                return;
            }

            ImageFile imageFile = new(file.Path);
            _navigationService.Navigate(CaptureToolNavigationRoutes.ImageEdit, imageFile);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void NavigateToSettings()
    {
        string activityId = ActivityIds.NavigateToSettings;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _navigationService.Navigate(CaptureToolNavigationRoutes.Settings);
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void ShowAboutApp()
    {
        string activityId = ActivityIds.ShowAboutApp;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _navigationService.Navigate(CaptureToolNavigationRoutes.About);
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void ExitApplication()
    {
        string activityId = ActivityIds.ExitApplication;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _appController.Shutdown();
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }
}
