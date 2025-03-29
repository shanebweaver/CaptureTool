using System;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Services.Navigation;

namespace CaptureTool.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, INavigationHandler
{
    private readonly INavigationService _navigationService;

    public event Action<NavigationRequest>? NavigationRequested;

    public MainWindowViewModel(
        INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public void HandleNavigationRequest(NavigationRequest request)
    {
        NavigationRequested?.Invoke(request);
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        _navigationService.SetNavigationHandler(this);
        _navigationService.Navigate(NavigationKeys.Home);
        await base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        base.Unload();
    }
}
