using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.ViewModels.Helpers;

namespace CaptureTool.ViewModels;

public sealed partial class LoadingPageViewModel : ViewModelBase
{
    public readonly struct ActivityIds
    {
        public static readonly string GoBack = "GoBack";
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
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.GoBack, () =>
        {
            _appNavigation.GoBackOrGoHome();
        });
    }
}