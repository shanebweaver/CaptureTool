using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.Home.ShowHomePage;

public sealed class ShowHomePageUseCase : IUseCase<ShowHomePageRequest, ShowHomePageResponse>
{
    private readonly INavigationService _navigationService;

    public ShowHomePageUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<ShowHomePageResponse> ExecuteAsync(ShowHomePageRequest request, CancellationToken cancellationToken = default)
    {
        _navigationService.Navigate(NavigationRoute.Home, clearHistory: true);
        return Task.FromResult(new ShowHomePageResponse());
    }
}