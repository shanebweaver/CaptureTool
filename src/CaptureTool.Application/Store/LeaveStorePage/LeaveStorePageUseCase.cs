using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Store.LeaveStorePage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Store.LeaveStorePage;

internal sealed class LeaveStorePageUseCase : ILeaveStorePageUseCase
{
    private const string ActivityId = "LeaveStorePage";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationCoordinator _navigationCoordinator;

    public LeaveStorePageUseCase(INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationCoordinator = navigationCoordinator;
    }

    public Task<UseCaseResponse<LeaveStorePageResponse>> ExecuteAsync(LeaveStorePageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async token =>
            {
                await _navigationCoordinator.ExecuteTransitionAsync(
                    async transitionToken => await _navigationCoordinator.TryGoBackAsync(transitionToken)
                        || await _navigationCoordinator.NavigateAsync(
                            NavigationRoute.Home,
                            clearHistory: true,
                            cancellationToken: transitionToken),
                    token);

                return new LeaveStorePageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
