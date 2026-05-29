using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.About.OpenAboutPage;

public sealed class OpenAboutPageUseCase : IUseCase<OpenAboutPageRequest, OpenAboutPageResponse>
{
    private readonly INavigationService _navigationService;

    public OpenAboutPageUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<OpenAboutPageResponse> ExecuteAsync(OpenAboutPageRequest request, CancellationToken cancellationToken = default)
    {
        _navigationService.Navigate(NavigationRoute.About);
        return Task.FromResult(new OpenAboutPageResponse());
    }
}
