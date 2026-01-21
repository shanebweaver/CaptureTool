using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Common.Commands.Extensions;
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

    private const string TelemetryContext = "ErrorPage";

    private readonly IErrorRestartAppAction _restartAppAction;
    private readonly ITelemetryService _telemetryService;

    public RelayCommand RestartAppCommand { get; }

    public ErrorPageViewModel(
        IErrorRestartAppAction restartAppAction,
        ITelemetryService telemetryService)
    {
        _restartAppAction = restartAppAction;
        _telemetryService = telemetryService;

        TelemetryCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        RestartAppCommand = commandFactory.Create(ActivityIds.RestartApp, RestartApp);
    }

    private void RestartApp()
    {
        _restartAppAction.ExecuteCommand();
    }
}
