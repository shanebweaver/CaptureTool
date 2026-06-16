using CaptureTool.Application.Abstractions.Features.Home.ShowHomePage;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.Home.ShowHomePage;

public sealed class ShowHomePageUseCase : IShowHomePageUseCase
{
    private const string ActivityId = "ShowHomePage";

    private readonly INavigationService _navigationService;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public ShowHomePageUseCase(
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _navigationService = navigationService;
        _useCaseExecutor = useCaseExecutor;
    }

    public Task<UseCaseResponse<ShowHomePageResponse>> ExecuteAsync(ShowHomePageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
                return new ShowHomePageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
