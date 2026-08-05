using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Shell.About.LeaveAboutPage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Shell.About.LeaveAboutPage;

internal sealed class LeaveAboutPageUseCase : ILeaveAboutPageUseCase
{
    private const string ActivityId = "LeaveAboutPage";

    private readonly INavigationCoordinator _navigationCoordinator;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public LeaveAboutPageUseCase(
        INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor)
    {
        _navigationCoordinator = navigationCoordinator;
        _useCaseExecutor = useCaseExecutor;
    }

    public Task<UseCaseResponse<LeaveAboutPageResponse>> ExecuteAsync(LeaveAboutPageRequest request, CancellationToken cancellationToken = default)
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

                return new LeaveAboutPageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
