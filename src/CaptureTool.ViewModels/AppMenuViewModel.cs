using CaptureTool.Capture.Desktop;
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
        public static readonly string Load = "Load";
        public static readonly string Unload = "Unload";
        public static readonly string NewDesktopImageCapture = "NewDesktopImageCapture";
        public static readonly string NewDesktopVideoCapture = "NewDesktopVideoCapture";
        public static readonly string NewDesktopAudioCapture = "NewDesktopAudioCapture";
        public static readonly string OpenFile = "OpenFile";
        public static readonly string NavigateToSettings = "NavigateToSettings";
        public static readonly string ShowAboutApp = "ShowAboutApp";
        public static readonly string ExitApplication = "ExitApplication";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly ICancellationService _cancellationService;
    private readonly INavigationService _navigationService;
    private readonly IAppController _appController;
    private readonly IFeatureManager _featureManager;

    public event EventHandler? ShowAboutAppRequested;

    public RelayCommand NewDesktopImageCaptureCommand => new(NewDesktopImageCapture, () => IsDesktopImageCaptureEnabled);
    public RelayCommand NewDesktopVideoCaptureCommand => new(NewDesktopVideoCapture, () => IsDesktopVideoCaptureEnabled);
    public RelayCommand NewAudioCaptureCommand => new(NewDesktopAudioCapture, () => IsDesktopAudioCaptureEnabled);
    public RelayCommand OpenFileCommand => new(OpenFile);
    public RelayCommand NavigateToSettingsCommand => new(NavigateToSettings);
    public RelayCommand ShowAboutAppCommand => new(ShowAboutApp);
    public RelayCommand ExitApplicationCommand => new(ExitApplication);

    private bool _isDesktopImageCaptureEnabled;
    public bool IsDesktopImageCaptureEnabled
    {
        get => _isDesktopImageCaptureEnabled;
        set => Set(ref _isDesktopImageCaptureEnabled, value);
    }

    private bool _isDesktopVideoCaptureEnabled;
    public bool IsDesktopVideoCaptureEnabled
    {
        get => _isDesktopVideoCaptureEnabled;
        set => Set(ref _isDesktopVideoCaptureEnabled, value);
    }

    private bool _isDesktopAudioCaptureEnabled;
    public bool IsDesktopAudioCaptureEnabled
    {
        get => _isDesktopAudioCaptureEnabled;
        set => Set(ref _isDesktopAudioCaptureEnabled, value);
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
            IsDesktopImageCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Image);
            IsDesktopVideoCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Video);
            IsDesktopAudioCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Audio);
        
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
            IsDesktopAudioCaptureEnabled = false;
            IsDesktopImageCaptureEnabled = false;
            IsDesktopVideoCaptureEnabled = false;

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }

        base.Unload();
    }

    private void NewDesktopImageCapture()
    {
        string activityId = ActivityIds.NewDesktopImageCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            DesktopImageCaptureOptions options = new(DesktopImageCaptureMode.Rectangle, ImageFileType.Png, true);
            _ = _appController.NewDesktopImageCaptureAsync(options);
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void NewDesktopVideoCapture()
    {
        string activityId = ActivityIds.NewDesktopVideoCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            DesktopVideoCaptureOptions options = new(DesktopVideoCaptureMode.Rectangle, VideoFileType.Mp4, true);
            _ = _appController.NewDesktopVideoCaptureAsync(options);
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void NewDesktopAudioCapture()
    {
        string activityId = ActivityIds.NewDesktopAudioCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _ = _appController.NewDesktopAudioCaptureAsync(); 
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
            ShowAboutAppRequested?.Invoke(this, EventArgs.Empty);
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
