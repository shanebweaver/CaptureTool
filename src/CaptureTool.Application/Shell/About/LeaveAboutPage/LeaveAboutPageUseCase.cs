using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Shell.About.LeaveAboutPage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Shell.About.LeaveAboutPage;

internal sealed class LeaveAboutPageUseCase : ILeaveAboutPageUseCase
{
    private const string ActivityId = "LeaveAboutPage";

    private readonly INavigationService _navigationService;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public LeaveAboutPageUseCase(
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _navigationService = navigationService;
        _useCaseExecutor = useCaseExecutor;
    }

    public Task<UseCaseResponse<LeaveAboutPageResponse>> ExecuteAsync(LeaveAboutPageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                if (!_navigationService.TryGoBack())
                {
                    _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
                }

                return new LeaveAboutPageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
