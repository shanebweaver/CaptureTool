using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings.LeaveSettingsPage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.LeaveSettingsPage;

internal sealed class LeaveSettingsPageUseCase : ILeaveSettingsPageUseCase
{
    private const string ActivityId = "LeaveSettingsPage";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationCoordinator _navigationCoordinator;

    public LeaveSettingsPageUseCase(INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationCoordinator = navigationCoordinator;
    }

    public Task<UseCaseResponse<LeaveSettingsPageResponse>> ExecuteAsync(LeaveSettingsPageRequest request, CancellationToken cancellationToken = default)
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

                return new LeaveSettingsPageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
