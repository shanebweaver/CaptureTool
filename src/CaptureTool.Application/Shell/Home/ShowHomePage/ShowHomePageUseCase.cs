using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Shell.Home.ShowHomePage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Shell.Home.ShowHomePage;

internal sealed class ShowHomePageUseCase : IShowHomePageUseCase
{
    private const string ActivityId = "ShowHomePage";

    private readonly INavigationCoordinator _navigationCoordinator;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public ShowHomePageUseCase(
        INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor)
    {
        _navigationCoordinator = navigationCoordinator;
        _useCaseExecutor = useCaseExecutor;
    }

    public Task<UseCaseResponse<ShowHomePageResponse>> ExecuteAsync(ShowHomePageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                bool navigated = await _navigationCoordinator.NavigateAsync(
                    NavigationRoute.Home,
                    clearHistory: true,
                    cancellationToken: cancellationToken);
                return new ShowHomePageResponse(navigated);
            },
            cancellationToken: cancellationToken);
    }
}
