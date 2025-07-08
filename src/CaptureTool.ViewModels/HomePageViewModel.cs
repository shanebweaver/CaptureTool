using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Telemetry;
using System;

namespace CaptureTool.ViewModels;

public sealed partial class HomePageViewModel : LoadableViewModelBase
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
    

    private bool _isVideoCaptureEnabled;
    public bool IsVideoCaptureEnabled
    {
        get => _isVideoCaptureEnabled;
        set => Set(ref _isVideoCaptureEnabled, value);
    }

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
            _appController.ShowCaptureOverlay();
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
            throw new NotImplementedException();
            //_telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }
}
