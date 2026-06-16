using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.Settings.LeaveSettingsPage;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.LeaveSettingsPage;

public sealed class LeaveSettingsPageUseCase : ILeaveSettingsPageUseCase
{
    private const string ActivityId = "LeaveSettingsPage";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;

    public LeaveSettingsPageUseCase(INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
    }

    public Task<UseCaseResponse<LeaveSettingsPageResponse>> ExecuteAsync(LeaveSettingsPageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                if (!_navigationService.TryGoBack())
                {
                    _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
                }

                return new LeaveSettingsPageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
