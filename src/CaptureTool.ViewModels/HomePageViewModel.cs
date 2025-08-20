using CaptureTool.Capture;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Telemetry;
using System;

namespace CaptureTool.ViewModels;

public sealed partial class HomePageViewModel : ViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string NewImageCapture = "HomePageViewModel_NewImageCapture";
        public static readonly string NewVideoCapture = "HomePageViewModel_NewVideoCapture";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly IAppController _appController;

    public RelayCommand NewImageCaptureCommand => new(NewImageCapture);
    public RelayCommand NewVideoCaptureCommand => new(NewVideoCapture, () => IsVideoCaptureEnabled);

    public bool IsVideoCaptureEnabled { get; }

    public HomePageViewModel(
        IFeatureManager featureManager,
        ITelemetryService telemetryService,
        IAppController appController)
    {
        _telemetryService = telemetryService;
        _appController = appController;

        IsVideoCaptureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);
    }

    private void NewImageCapture()
    {
        string activityId = ActivityIds.NewImageCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            CaptureOptions options = new(CaptureMode.Image, CaptureType.Rectangle);
            _appController.ShowCaptureOverlay(CaptureOptions.ImageDefault);
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
            _appController.ShowCaptureOverlay(CaptureOptions.VideoDefault);
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }
}
