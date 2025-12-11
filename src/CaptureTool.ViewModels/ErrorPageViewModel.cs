using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Services.Interfaces.Shutdown;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.ViewModels.Helpers;

namespace CaptureTool.ViewModels;

public sealed partial class ErrorPageViewModel : ViewModelBase
{
    public readonly struct ActivityIds
    {
        public static readonly string RestartApp = "RestartApp";
    }

    private readonly IShutdownHandler _shutdownHandler;
    private readonly ITelemetryService _telemetryService;

    public RelayCommand RestartAppCommand { get; }

    public ErrorPageViewModel(
        IShutdownHandler shutdownHandler,
        ITelemetryService telemetryService)
    {
        _shutdownHandler = shutdownHandler;
        _telemetryService = telemetryService;

        RestartAppCommand = new(RestartApp);
    }

    private void RestartApp()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.RestartApp, () =>
        {
            _shutdownHandler.TryRestart();
        });
    }
}
