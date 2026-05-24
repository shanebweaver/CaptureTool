using CaptureTool.Presentation.ViewModels.Helpers;
using CaptureTool.Application.Abstractions.UseCases.Loading;
using CaptureTool.Infrastructure.Implementations.UseCases.Extensions;
using CaptureTool.Infrastructure.Implementations.ViewModels;
using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.Telemetry;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class LoadingPageViewModel : ViewModelBase
{
    public readonly struct ActivityIds
    {
        public static readonly string GoBack = "GoBack";
    }

    private const string TelemetryContext = "LoadingPage";

    private readonly ILoadingGoBackUseCase _goBackAction;

    public IAppCommand GoBackCommand { get; }

    public LoadingPageViewModel(
        ILoadingGoBackUseCase goBackAction,
        ITelemetryService telemetryService)
    {
        _goBackAction = goBackAction;

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        GoBackCommand = commandFactory.Create(ActivityIds.GoBack, GoBack);
    }

    private void GoBack()
    {
        _goBackAction.ExecuteCommand();
    }
}