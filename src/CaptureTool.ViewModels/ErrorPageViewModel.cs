using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Services.Telemetry;

namespace CaptureTool.ViewModels;

public sealed partial class ErrorPageViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string RestartApp = "ErrorPageViewModel_RestartApp";
    }

    private readonly IAppController _appController;
    private readonly ITelemetryService _telemetryService;

    public RelayCommand RestartAppCommand { get; }

    public ErrorPageViewModel(
        IAppController appController,
        ITelemetryService telemetryService)
    {
        _appController = appController;
        _telemetryService = telemetryService;

        RestartAppCommand = new(RestartApp);
    }

    private void RestartApp()
    {
        _telemetryService.ExecuteActivity(ActivityIds.RestartApp, async () =>
        {
            _appController.TryRestart();
        });
    }
}
