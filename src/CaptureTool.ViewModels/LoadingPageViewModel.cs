using CaptureTool.Capture;
using CaptureTool.Common.Commands;
using CaptureTool.Core.Navigation;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Telemetry;
using System;

namespace CaptureTool.ViewModels;

public sealed partial class LoadingPageViewModel : ViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string GoBack = "LoadingPageViewModel_GoBack";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly INavigationService _navigationService;

    public RelayCommand GoBackCommand => new(GoBack);

    public LoadingPageViewModel(
        ITelemetryService telemetryService,
        INavigationService navigationService)
    {
        _telemetryService = telemetryService;
        _navigationService = navigationService;
    }

    private void GoBack()
    {
        string activityId = ActivityIds.GoBack;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            if (!_navigationService.CanGoBack || !_navigationService.TryGoBack())
            {
                _navigationService.GoHome();
            }
            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }
}
