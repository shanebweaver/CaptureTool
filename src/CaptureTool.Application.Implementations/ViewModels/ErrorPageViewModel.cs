using CaptureTool.Application.Implementations.ViewModels.Helpers;
using CaptureTool.Application.Interfaces.UseCases.Error;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Common;
using CaptureTool.Infrastructure.Implementations.UseCases.Extensions;
using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.Telemetry;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class ErrorPageViewModel : ViewModelBase, IErrorPageViewModel
{
    public readonly struct ActivityIds
    {
        public static readonly string RestartApp = "RestartApp";
    }

    private const string TelemetryContext = "ErrorPage";

    private readonly IErrorRestartAppUseCase _restartAppAction;
    private readonly ITelemetryService _telemetryService;

    public IAppCommand RestartAppCommand { get; }

    public ErrorPageViewModel(
        IErrorRestartAppUseCase restartAppAction,
        ITelemetryService telemetryService)
    {
        _restartAppAction = restartAppAction;
        _telemetryService = telemetryService;

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        RestartAppCommand = commandFactory.Create(ActivityIds.RestartApp, RestartApp);
    }

    private void RestartApp()
    {
        _restartAppAction.ExecuteCommand();
    }
}
