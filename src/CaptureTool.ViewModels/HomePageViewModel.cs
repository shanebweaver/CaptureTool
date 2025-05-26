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

namespace CaptureTool.ViewModels;

public sealed partial class HomePageViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "HomePageViewModel_Load";
        public static readonly string Unload = "HomePageViewModel_Unload";
        public static readonly string NewDesktopImageCapture = "HomePageViewModel_NewDesktopImageCapture";
        public static readonly string NewDesktopVideoCapture = "HomePageViewModel_NewDesktopVideoCapture";
        public static readonly string NewDesktopAudioCapture = "HomePageViewModel_NewDesktopAudioCapture";
        public static readonly string DesktopImageCaptureOptions = "HomePageViewModel_DesktopImageCaptureOptions";
        public static readonly string DesktopVideoCaptureOptions = "HomePageViewModel_DesktopVideoCaptureOptions";
        public static readonly string DesktopAudioCaptureOptions = "HomePageViewModel_DesktopAudioCaptureOptions";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly IFeatureManager _featureManager;
    private readonly INavigationService _navigationService;

    public RelayCommand NewDesktopImageCaptureCommand => new(NewDesktopImageCapture, () => IsDesktopImageCaptureEnabled);
    public RelayCommand NewDesktopVideoCaptureCommand => new(NewDesktopVideoCapture, () => IsDesktopVideoCaptureEnabled);
    public RelayCommand NewDesktopAudioCaptureCommand => new(NewDesktopAudioCapture, () => IsDesktopAudioCaptureEnabled);
    public RelayCommand DesktopImageCaptureOptionsCommand => new(DesktopImageCaptureOptions, () => IsDesktopImageCaptureOptionsEnabled);
    public RelayCommand DesktopVideoCaptureOptionsCommand => new(DesktopVideoCaptureOptions, () => IsDesktopVideoCaptureOptionsEnabled);
    public RelayCommand DesktopAudioCaptureOptionsCommand => new(DesktopAudioCaptureOptions, () => IsDesktopAudioCaptureOptionsEnabled);

    private bool _isDesktopImageCaptureEnabled;
    public bool IsDesktopImageCaptureEnabled
    {
        get => _isDesktopImageCaptureEnabled;
        set => Set(ref _isDesktopImageCaptureEnabled, value);
    }

    private bool _isDesktopImageCaptureOptionsEnabled;
    public bool IsDesktopImageCaptureOptionsEnabled
    {
        get => _isDesktopImageCaptureOptionsEnabled;
        set => Set(ref _isDesktopImageCaptureOptionsEnabled, value);
    }

    private bool _isDesktopVideoCaptureEnabled;
    public bool IsDesktopVideoCaptureEnabled
    {
        get => _isDesktopVideoCaptureEnabled;
        set => Set(ref _isDesktopVideoCaptureEnabled, value);
    }

    private bool _isDesktopVideoCaptureOptionsEnabled;
    public bool IsDesktopVideoCaptureOptionsEnabled
    {
        get => _isDesktopVideoCaptureOptionsEnabled;
        set => Set(ref _isDesktopVideoCaptureOptionsEnabled, value);
    }

    private bool _isDesktopAudioCaptureEnabled;
    public bool IsDesktopAudioCaptureEnabled
    {
        get => _isDesktopAudioCaptureEnabled;
        set => Set(ref _isDesktopAudioCaptureEnabled, value);
    }

    private bool _isDesktopAudioCaptureOptionsEnabled;
    public bool IsDesktopAudioCaptureOptionsEnabled
    {
        get => _isDesktopAudioCaptureOptionsEnabled;
        set => Set(ref _isDesktopAudioCaptureOptionsEnabled, value);
    }

    public HomePageViewModel(
        ITelemetryService telemetryService,
        IAppController appController,
        ICancellationService cancellationService,
        IFeatureManager featureManager,
        INavigationService navigationService)
    {
        _telemetryService = telemetryService;
        _appController = appController;
        _cancellationService = cancellationService;
        _featureManager = featureManager;
        _navigationService = navigationService;
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

            // Desktop Image
            IsDesktopImageCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Image);
            IsDesktopImageCaptureOptionsEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Image_Options);

            // Desktop Video
            IsDesktopVideoCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Video);
            IsDesktopVideoCaptureOptionsEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Video_Options);

            // Desktop Audio
            IsDesktopAudioCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Audio);
            IsDesktopAudioCaptureOptionsEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_DesktopCapture_Audio_Options);

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

    public override void Unload()
    {
        string activityId = ActivityIds.Unload;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _isDesktopImageCaptureEnabled = false;
            _isDesktopVideoCaptureEnabled = false;
            _isDesktopAudioCaptureEnabled = false;

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }

        base.Unload();
    }

    private async void NewDesktopImageCapture()
    {
        string activityId = ActivityIds.NewDesktopImageCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            // TODO: Remember last used options
            ImageCaptureOptions options = new(ImageCaptureMode.Rectangle, ImageFileType.Png, true);
            await _appController.NewDesktopImageCaptureAsync(options);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private async void NewDesktopVideoCapture()
    {
        string activityId = ActivityIds.NewDesktopVideoCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            // TODO: Remember last used options
            VideoCaptureOptions options = new(VideoCaptureMode.Rectangle, VideoFileType.Mp4, true);
            await _appController.NewDesktopVideoCaptureAsync(options);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private async void NewDesktopAudioCapture()
    {
        string activityId = ActivityIds.NewDesktopAudioCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            await _appController.NewDesktopAudioCaptureAsync();

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void DesktopImageCaptureOptions()
    {
        string activityId = ActivityIds.DesktopImageCaptureOptions;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _navigationService.Navigate(CaptureToolNavigationRoutes.DesktopImageCaptureOptions, null);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void DesktopVideoCaptureOptions()
    {
        string activityId = ActivityIds.DesktopVideoCaptureOptions;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _navigationService.Navigate(CaptureToolNavigationRoutes.DesktopVideoCaptureOptions, null);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void DesktopAudioCaptureOptions()
    {
        string activityId = ActivityIds.DesktopAudioCaptureOptions;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            // Not implemented yet
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }
}
