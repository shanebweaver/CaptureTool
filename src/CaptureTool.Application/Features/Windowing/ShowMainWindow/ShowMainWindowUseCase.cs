using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.Windowing.ShowMainWindow;

public sealed class ShowMainWindowUseCase : IUseCase<ShowMainWindowRequest, ShowMainWindowResponse>, IConditional<ShowMainWindowRequest>
{
    private readonly INavigationService _navigationService;

    public ShowMainWindowUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<bool> CanExecuteAsync(ShowMainWindowRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_navigationService.CanGoBack);
    }

    public Task<ShowMainWindowResponse> ExecuteAsync(ShowMainWindowRequest request, CancellationToken cancellationToken = default)
    {
        bool success = _navigationService.TryGoBackTo(r => CaptureToolNavigationRouteHelper.IsMainWindowRoute(r.Route));
        if (!success)
        {
            _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
        }

        return Task.FromResult(new ShowMainWindowResponse());
    }
}