using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Error;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.ViewModels.Helpers;

namespace CaptureTool.ViewModels;

public sealed partial class ErrorPageViewModel : ViewModelBase
{
    public readonly struct ActivityIds
    {
        public static readonly string RestartApp = "RestartApp";
    }

    private readonly IErrorActions _errorActions;
    private readonly ITelemetryService _telemetryService;

    public RelayCommand RestartAppCommand { get; }

    public ErrorPageViewModel(
        IErrorActions errorActions,
        ITelemetryService telemetryService)
    {
        _errorActions = errorActions;
        _telemetryService = telemetryService;

        RestartAppCommand = new(RestartApp);
    }

    private void RestartApp()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.RestartApp, () =>
        {
            _errorActions.RestartApp();
        });
    }
}
