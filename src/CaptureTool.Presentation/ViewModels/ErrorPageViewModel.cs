using CaptureTool.Application.Abstractions.Error;
using CaptureTool.Infrastructure.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class ErrorPageViewModel : ViewModelBase
{
    public IRelayCommand RestartAppCommand { get; }

    public ErrorPageViewModel(
        IRestartApplicationAppCommand restartAppAction)
    {
        RestartAppCommand = restartAppAction.ToRelayCommand();
    }
}
