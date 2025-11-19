using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Core.Telemetry;
using CaptureTool.Services.Telemetry;

namespace CaptureTool.ViewModels;

public sealed partial class ErrorPageViewModel : ViewModelBase
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
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.RestartApp, async () =>
        {
            _appController.TryRestart();
        });
    }
}
