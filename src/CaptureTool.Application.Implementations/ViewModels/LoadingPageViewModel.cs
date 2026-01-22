using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Common.Commands.Extensions;
using CaptureTool.Application.Interfaces.Actions.Loading;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using CaptureTool.Application.Implementations.ViewModels.Helpers;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class LoadingPageViewModel : ViewModelBase, ILoadingPageViewModel
{
    public readonly struct ActivityIds
    {
        public static readonly string GoBack = "GoBack";
    }

    private const string TelemetryContext = "LoadingPage";

    private readonly ILoadingGoBackAction _goBackAction;
    private readonly ITelemetryService _telemetryService;

    public RelayCommand GoBackCommand { get; }

    public LoadingPageViewModel(
        ILoadingGoBackAction goBackAction,
        ITelemetryService telemetryService)
    {
        _goBackAction = goBackAction;
        _telemetryService = telemetryService;

        TelemetryCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        GoBackCommand = commandFactory.Create(ActivityIds.GoBack, GoBack);
    }

    private void GoBack()
    {
        _goBackAction.ExecuteCommand();
    }
}