using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;

namespace CaptureTool.ViewModels;

public sealed partial class AppMenuViewModel : ViewModelBase
{
    private readonly ILogService _logService;
    private readonly INavigationService _navigationService;

    public RelayCommand GoToSettingsCommand => new(GoToSettings);

    public AppMenuViewModel(
        ILogService logService,
        INavigationService navigationService)
    {
        _logService = logService;
        _navigationService = navigationService;
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        _logService.LogInformation("Loading AppMenuViewModel");
        return base.LoadAsync(parameter, cancellationToken);
    }

    override public void Unload()
    {
        _logService.LogInformation("Unloading AppMenuViewModel");
        base.Unload();
    }

    private void GoToSettings()
    {
        _navigationService.Navigate(NavigationKeys.Settings);
    }
}
