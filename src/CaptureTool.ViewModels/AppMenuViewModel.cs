using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Capture.Desktop;
using CaptureTool.Core;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Telemetry;
using CaptureTool.ViewModels.Commands;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CaptureTool.ViewModels;

public sealed partial class AppMenuViewModel : ViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "Load";
        public static readonly string Unload = "Unload";
        public static readonly string NewDesktopCapture = "NewDesktopCapture";
        public static readonly string NewAudioCapture = "NewAudioCapture";
        public static readonly string NewCameraCapture = "NewCameraCapture";
        public static readonly string OpenFile = "OpenFile";
        public static readonly string NavigateToSettings = "NavigateToSettings";
        public static readonly string ShowAboutDialog = "ShowAboutDialog";
        public static readonly string ExitApplication = "ExitApplication";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly ICancellationService _cancellationService;
    private readonly INavigationService _navigationService;
    private readonly IAppController _appController;
    private readonly IFeatureManager _featureManager;

    public RelayCommand NewDesktopCaptureCommand => new(NewDesktopCapture, () => IsDesktopCaptureEnabled);
    public RelayCommand NewAudioCaptureCommand => new(NewAudioCapture, () => IsAudioCaptureEnabled);
    public RelayCommand NewCameraCaptureCommand => new(NewCameraCapture, () => IsCameraCaptureEnabled);
    public RelayCommand OpenFileCommand => new(OpenFile);
    public RelayCommand NavigateToSettingsCommand => new(NavigateToSettings);
    public RelayCommand ShowAboutDialogCommand => new(ShowAboutDialog);
    public RelayCommand ExitApplicationCommand => new(ExitApplication);

    private bool _isDesktopCaptureEnabled;
    public bool IsDesktopCaptureEnabled
    {
        get => _isDesktopCaptureEnabled;
        set => Set(ref _isDesktopCaptureEnabled, value);
    }

    private bool _isAudioCaptureEnabled;
    public bool IsAudioCaptureEnabled
    {
        get => _isAudioCaptureEnabled;
        set => Set(ref _isAudioCaptureEnabled, value);
    }

    private bool _isCameraCaptureEnabled;
    public bool IsCameraCaptureEnabled
    {
        get => _isCameraCaptureEnabled;
        set => Set(ref _isCameraCaptureEnabled, value);
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
        Unload();
        Debug.Assert(IsUnloaded);
        StartLoading();

        string activityId = ActivityIds.Load;
        _telemetryService.ActivityInitiated(activityId);

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            IsDesktopCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture);
            IsAudioCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_AudioCapture);
            IsCameraCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_CameraCapture);
        
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (OperationCanceledException)
        {
            _telemetryService.ActivityCanceled(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
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
            // cleanup here
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }

        base.Unload();
    }

    private void NewDesktopCapture()
    {
        string activityId = ActivityIds.NewDesktopCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            DesktopCaptureOptions options = new(DesktopImageCaptureMode.Rectangle, ImageFileType.Png, true);
            _ = _appController.NewDesktopCaptureAsync(options);
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void NewCameraCapture()
    {
        string activityId = ActivityIds.NewCameraCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _ = _appController.NewCameraCaptureAsync();
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
            _navigationService.Navigate(NavigationRoutes.ImageEdit, imageFile);

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
            _navigationService.Navigate(NavigationRoutes.Settings);
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void ShowAboutDialog()
    {
        string activityId = ActivityIds.ShowAboutDialog;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _navigationService.Navigate(NavigationRoutes.About); 
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
