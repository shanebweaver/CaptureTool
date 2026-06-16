using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.Windowing.ShowMainWindow;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.Windowing.ShowMainWindow;

public sealed class ShowMainWindowUseCase : IShowMainWindowUseCase
{
    private const string ActivityId = "ShowMainWindow";

    private readonly INavigationService _navigationService;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public ShowMainWindowUseCase(
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _navigationService = navigationService;
        _useCaseExecutor = useCaseExecutor;
    }

    public bool CanExecute(ShowMainWindowRequest request)
    {
        return _navigationService.CanGoBack;
    }

    public Task<UseCaseResponse<ShowMainWindowResponse>> ExecuteAsync(ShowMainWindowRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                bool success = _navigationService.TryGoBackTo(r => CaptureToolNavigationRouteHelper.IsMainWindowRoute(r.Route));
                if (!success)
                {
                    _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
                }

                return new ShowMainWindowResponse();
            },
            cancellationToken: cancellationToken);
    }
}
