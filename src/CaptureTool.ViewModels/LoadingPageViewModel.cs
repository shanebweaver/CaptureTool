using CaptureTool.Common.Commands;
using CaptureTool.Core.Navigation;
using CaptureTool.Services.Telemetry;
using System;

namespace CaptureTool.ViewModels;

public sealed partial class LoadingPageViewModel : ViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string GoBack = "LoadingPageViewModel_GoBack";
    }

    private readonly IAppNavigation _appNavigation;
    private readonly ITelemetryService _telemetryService;

    public RelayCommand GoBackCommand { get; }

    public LoadingPageViewModel(
        IAppNavigation appNavigation,
        ITelemetryService telemetryService)
    {
        _appNavigation = appNavigation;
        _telemetryService = telemetryService;

        GoBackCommand = new(GoBack);
    }

    private void GoBack()
    {
        _telemetryService.ExecuteActivity(ActivityIds.GoBack, async () =>
        {
            _appNavigation.GoBackOrGoHome();
        });
    }
}