using CaptureTool.Capture;
using CaptureTool.Common.Commands;
using CaptureTool.Core.Navigation;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Navigation;
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

    private readonly AppNavigation _appNavigation;
    private readonly ITelemetryService _telemetryService;

    public RelayCommand NewImageCaptureCommand => new(NewImageCapture);
    public RelayCommand NewVideoCaptureCommand => new(NewVideoCapture, () => IsVideoCaptureEnabled);

    public bool IsVideoCaptureEnabled { get; }

    public HomePageViewModel(
        AppNavigation appNavigation,
        IFeatureManager featureManager,
        ITelemetryService telemetryService)
    {
        _appNavigation = appNavigation;
        _telemetryService = telemetryService;

        IsVideoCaptureEnabled = featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);
    }

    private void NewImageCapture()
    {
        string activityId = ActivityIds.NewImageCapture;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
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
            _appNavigation.GoToImageCapture(CaptureOptions.VideoDefault);
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }
}
