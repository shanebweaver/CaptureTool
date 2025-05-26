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
        public static readonly string NewImageCapture = "HomePageViewModel_NewImageCapture";
        public static readonly string NewVideoCapture = "HomePageViewModel_NewVideoCapture";
        public static readonly string NewAudioCapture = "HomePageViewModel_NewAudioCapture";
        public static readonly string ImageCaptureOptions = "HomePageViewModel_ImageCaptureOptions";
        public static readonly string VideoCaptureOptions = "HomePageViewModel_VideoCaptureOptions";
        public static readonly string AudioCaptureOptions = "HomePageViewModel_AudioCaptureOptions";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly IFeatureManager _featureManager;
    private readonly INavigationService _navigationService;

    public RelayCommand NewImageCaptureCommand => new(NewImageCapture, () => IsImageCaptureEnabled);
    public RelayCommand NewVideoCaptureCommand => new(NewVideoCapture, () => IsVideoCaptureEnabled);
    public RelayCommand NewAudioCaptureCommand => new(NewAudioCapture, () => IsAudioCaptureEnabled);
    public RelayCommand ImageCaptureOptionsCommand => new(ImageCaptureOptions, () => IsImageCaptureOptionsEnabled);
    public RelayCommand VideoCaptureOptionsCommand => new(VideoCaptureOptions, () => IsVideoCaptureOptionsEnabled);
    public RelayCommand AudioCaptureOptionsCommand => new(AudioCaptureOptions, () => IsAudioCaptureOptionsEnabled);

    private bool _isImageCaptureEnabled;
    public bool IsImageCaptureEnabled
    {
        get => _isImageCaptureEnabled;
        set => Set(ref _isImageCaptureEnabled, value);
    }

    private bool _isImageCaptureOptionsEnabled;
    public bool IsImageCaptureOptionsEnabled
    {
        get => _isImageCaptureOptionsEnabled;
        set => Set(ref _isImageCaptureOptionsEnabled, value);
    }

    private bool _isVideoCaptureEnabled;
    public bool IsVideoCaptureEnabled
    {
        get => _isVideoCaptureEnabled;
        set => Set(ref _isVideoCaptureEnabled, value);
    }

    private bool _isVideoCaptureOptionsEnabled;
    public bool IsVideoCaptureOptionsEnabled
    {
        get => _isVideoCaptureOptionsEnabled;
        set => Set(ref _isVideoCaptureOptionsEnabled, value);
    }

    private bool _isAudioCaptureEnabled;
    public bool IsAudioCaptureEnabled
    {
        get => _isAudioCaptureEnabled;
        set => Set(ref _isAudioCaptureEnabled, value);
    }

    private bool _isAudioCaptureOptionsEnabled;
    public bool IsAudioCaptureOptionsEnabled
    {
        get => _isAudioCaptureOptionsEnabled;
        set => Set(ref _isAudioCaptureOptionsEnabled, value);
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

            //  Image
            IsImageCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Capture_Image);
            IsImageCaptureOptionsEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Capture_Image_Options);

            //  Video
            IsVideoCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Capture_Video);
            IsVideoCaptureOptionsEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Capture_Video_Options);

            //  Audio
            IsAudioCaptureEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Capture_Audio);
            IsAudioCaptureOptionsEnabled = await _featureManager.IsEnabledAsync(CaptureToolFeatures.Feature_Capture_Audio_Options);

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
            _isImageCaptureEnabled = false;
            _isVideoCaptureEnabled = false;
            _isAudioCaptureEnabled = false;

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }

        base.Unload();
    }

    private async void NewImageCapture()
    {
        string activityId = ActivityIds.NewImageCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            // TODO: Remember last used options
            ImageCaptureOptions options = new(ImageCaptureMode.Rectangle, ImageFileType.Png, true);
            await _appController.NewImageCaptureAsync(options);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private async void NewVideoCapture()
    {
        string activityId = ActivityIds.NewVideoCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            // TODO: Remember last used options
            VideoCaptureOptions options = new(VideoCaptureMode.Rectangle, VideoFileType.Mp4, true);
            await _appController.NewVideoCaptureAsync(options);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private async void NewAudioCapture()
    {
        string activityId = ActivityIds.NewAudioCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            await _appController.NewAudioCaptureAsync();

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void ImageCaptureOptions()
    {
        string activityId = ActivityIds.ImageCaptureOptions;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _navigationService.Navigate(CaptureToolNavigationRoutes.ImageCaptureOptions, null);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void VideoCaptureOptions()
    {
        string activityId = ActivityIds.VideoCaptureOptions;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _navigationService.Navigate(CaptureToolNavigationRoutes.VideoCaptureOptions, null);

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void AudioCaptureOptions()
    {
        string activityId = ActivityIds.AudioCaptureOptions;
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
