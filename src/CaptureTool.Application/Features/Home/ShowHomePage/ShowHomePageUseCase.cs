using CaptureTool.Application.Abstractions.Features.Home.ShowHomePage;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.Home.ShowHomePage;

public sealed class ShowHomePageUseCase : IShowHomePageUseCase
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