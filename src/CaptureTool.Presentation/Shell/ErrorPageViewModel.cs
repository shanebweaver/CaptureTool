using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.Error.RestartApplication;
using CaptureTool.Infrastructure.ViewModels;
using CaptureTool.Presentation.Shared.Commands;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Shell;

public sealed partial class ErrorPageViewModel : ViewModelBase
{
    public IRelayCommand RestartAppCommand { get; }

    public ErrorPageViewModel(
        IUseCase<RestartApplicationRequest, RestartApplicationResponse> restartAppAction)
    {
        RestartAppCommand = restartAppAction.ToRelayCommand(() => new RestartApplicationRequest());
    }
}
