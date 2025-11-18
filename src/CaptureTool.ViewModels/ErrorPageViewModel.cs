using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;

namespace CaptureTool.ViewModels;

public sealed partial class ErrorPageViewModel : LoadableViewModelBase
{
    private readonly IAppController _appController;

    public RelayCommand RestartAppCommand { get; }

    public ErrorPageViewModel(
        IAppController appController)
    {
        _appController = appController;

        RestartAppCommand = new(RestartApp);
    }

    private void RestartApp()
    {
        _appController.TryRestart();
    }
}
