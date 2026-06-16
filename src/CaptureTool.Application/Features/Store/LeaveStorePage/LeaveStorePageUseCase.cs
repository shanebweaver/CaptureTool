using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.Store.LeaveStorePage;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.Store.LeaveStorePage;

public sealed class LeaveStorePageUseCase : ILeaveStorePageUseCase
{
    private const string ActivityId = "LeaveStorePage";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;

    public LeaveStorePageUseCase(INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
    }

    public Task<UseCaseResponse<LeaveStorePageResponse>> ExecuteAsync(LeaveStorePageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                if (!_navigationService.TryGoBack())
                {
                    _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
                }

                return new LeaveStorePageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
