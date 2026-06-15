using CaptureTool.Application.Abstractions.Features.About.OpenAboutPage;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.About.OpenAboutPage;

public sealed class OpenAboutPageUseCase : IOpenAboutPageUseCase
{
    private readonly INavigationService _navigationService;

    public OpenAboutPageUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<OpenAboutPageResponse> ExecuteAsync(OpenAboutPageRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _navigationService.Navigate(NavigationRoute.About);
            return Task.FromResult(new OpenAboutPageResponse());
        }
        catch (Exception)
        {
            return Task.FromResult(new OpenAboutPageResponse(false));
        }
    }
}
