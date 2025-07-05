using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Telemetry;
using System;

namespace CaptureTool.ViewModels;

public sealed partial class HomePageViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string NewImageCapture = "HomePageViewModel_NewImageCapture";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly IAppController _appController;

    public RelayCommand NewImageCaptureCommand => new(NewImageCapture);

    private bool _isImageCaptureEnabled;
    public bool IsImageCaptureEnabled
    {
        get => _isImageCaptureEnabled;
        set => Set(ref _isImageCaptureEnabled, value);
    }

    public HomePageViewModel(
        ITelemetryService telemetryService,
        IAppController appController)
    {
        _telemetryService = telemetryService;
        _appController = appController;
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
}
