using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.Windowing.ShowMainWindow;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.Windowing.ShowMainWindow;

public sealed class ShowMainWindowUseCase : IShowMainWindowUseCase
{
    private readonly INavigationService _navigationService;

    public ShowMainWindowUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public bool CanExecute(ShowMainWindowRequest request)
    {
        return _navigationService.CanGoBack;
    }

    public Task<ShowMainWindowResponse> ExecuteAsync(ShowMainWindowRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            bool success = _navigationService.TryGoBackTo(r => CaptureToolNavigationRouteHelper.IsMainWindowRoute(r.Route));
            if (!success)
            {
                _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
            }

            return Task.FromResult(new ShowMainWindowResponse());
        }
        catch (Exception)
        {
            return Task.FromResult(new ShowMainWindowResponse(false));
        }
    }
}
