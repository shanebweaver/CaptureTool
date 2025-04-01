using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Core;
using CaptureTool.Services.Navigation;

namespace CaptureTool.ViewModels;

public sealed partial class AppMenuViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public RelayCommand GoToSettingsCommand => new(GoToSettings);

    public AppMenuViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public override Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Debug.Assert(IsUnloaded);
        StartLoading();
        return base.LoadAsync(parameter, cancellationToken);
    }

    override public void Unload()
    {
        base.Unload();
    }

    private void GoToSettings()
    {
        _navigationService.Navigate(NavigationKeys.Settings);
    }
}
