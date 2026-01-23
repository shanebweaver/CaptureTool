using CaptureTool.Application.Implementations.ViewModels.Helpers;
using CaptureTool.Application.Interfaces.UseCases.Loading;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Common;
using CaptureTool.Infrastructure.Implementations.UseCases.Extensions;
using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.Telemetry;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class LoadingPageViewModel : ViewModelBase, ILoadingPageViewModel
{
    public readonly struct ActivityIds
    {
        public static readonly string GoBack = "GoBack";
    }

    private const string TelemetryContext = "LoadingPage";

    private readonly ILoadingGoBackUseCase _goBackAction;
    private readonly ITelemetryService _telemetryService;

    public IAppCommand GoBackCommand { get; }

    public LoadingPageViewModel(
        ILoadingGoBackUseCase goBackAction,
        ITelemetryService telemetryService)
    {
        _goBackAction = goBackAction;
        _telemetryService = telemetryService;

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        GoBackCommand = commandFactory.Create(ActivityIds.GoBack, GoBack);
    }

    private void GoBack()
    {
        _goBackAction.ExecuteCommand();
    }
}