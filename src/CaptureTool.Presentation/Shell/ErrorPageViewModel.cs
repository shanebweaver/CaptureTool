using CaptureTool.Application.Abstractions.Features.Error.RestartApplication;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Shell;

public sealed partial class ErrorPageViewModel : ViewModelBase
{
    public IRelayCommand RestartAppCommand { get; }

    public ErrorPageViewModel(
        IRestartApplicationUseCase restartAppAction,
        ITelemetryService telemetryService)
    {
        RestartAppCommand = restartAppAction.ToRelayCommand(() => new RestartApplicationRequest(), telemetryService);
    }
}
