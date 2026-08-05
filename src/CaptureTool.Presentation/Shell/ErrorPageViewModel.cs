using CaptureTool.Application.Abstractions.Shell.Error.RestartApplication;
using CaptureTool.Application.Abstractions.Shell.AppMenu.ExitApplication;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Shell;

public sealed partial class ErrorPageViewModel : ViewModelBase
{
    public IAsyncRelayCommand RestartAppCommand { get; }
    public IRelayCommand ExitAppCommand { get; }

    public bool HasRestartFailed
    {
        get;
        private set => Set(ref field, value);
    }

    private readonly IRestartApplicationUseCase _restartAppAction;

    public ErrorPageViewModel(
        IRestartApplicationUseCase restartAppAction,
        IExitApplicationUseCase exitAppAction)
    {
        _restartAppAction = restartAppAction;
        RestartAppCommand = new AsyncRelayCommand(
            RestartAppAsync,
            () => restartAppAction.CanExecute(new RestartApplicationRequest()),
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ExitAppCommand = exitAppAction.ToRelayCommand(() => new ExitApplicationRequest());
    }

    private async Task RestartAppAsync()
    {
        HasRestartFailed = false;
        var response = await _restartAppAction.ExecuteAsync(
            new RestartApplicationRequest(),
            CancellationToken.None);
        HasRestartFailed = response.Value?.Succeeded != true;
    }
}
