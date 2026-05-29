using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.Error.RestartApplication;
using CaptureTool.Infrastructure.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class ErrorPageViewModel : ViewModelBase
{
    public IRelayCommand RestartAppCommand { get; }

    public ErrorPageViewModel(
        IUseCase<RestartApplicationRequest, RestartApplicationResponse> restartAppAction)
    {
        RestartAppCommand = restartAppAction.ToRelayCommand(() => new RestartApplicationRequest());
    }
}
