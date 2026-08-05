using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Abstractions.Windowing.ShowMainWindow;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Windowing.ShowMainWindow;

internal sealed class ShowMainWindowUseCase : IShowMainWindowUseCase
{
    private const string ActivityId = "ShowMainWindow";

    private readonly INavigationCoordinator _navigationCoordinator;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public ShowMainWindowUseCase(
        INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor)
    {
        _navigationCoordinator = navigationCoordinator;
        _useCaseExecutor = useCaseExecutor;
    }

    public bool CanExecute(ShowMainWindowRequest request)
    {
        return _navigationCoordinator.CanGoBack;
    }

    public Task<UseCaseResponse<ShowMainWindowResponse>> ExecuteAsync(ShowMainWindowRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async token =>
            {
                bool success = await _navigationCoordinator.ExecuteTransitionAsync(
                    async transitionToken =>
                    {
                        if (await _navigationCoordinator.TryGoBackToAsync(
                            r => CaptureToolNavigationRouteHelper.IsMainWindowRoute(r.Route),
                            transitionToken))
                        {
                            return true;
                        }

                        return request.CreateIfUnavailable && await _navigationCoordinator.NavigateAsync(
                            NavigationRoute.Home,
                            clearHistory: true,
                            cancellationToken: transitionToken);
                    },
                    token);

                return new ShowMainWindowResponse(success);
            },
            cancellationToken: cancellationToken);
    }
}
