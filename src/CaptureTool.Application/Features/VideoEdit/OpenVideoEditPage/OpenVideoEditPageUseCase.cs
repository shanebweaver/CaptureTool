using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.VideoEdit.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.VideoEdit.OpenVideoEditPage;

public sealed class OpenVideoEditPageUseCase : IOpenVideoEditPageUseCase
{
    private readonly INavigationService _navigationService;

    public OpenVideoEditPageUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<OpenVideoEditPageResponse> ExecuteAsync(OpenVideoEditPageRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _navigationService.Navigate(NavigationRoute.VideoEdit, request.VideoFile);
            return Task.FromResult(new OpenVideoEditPageResponse());
        }
        catch (Exception)
        {
            return Task.FromResult(new OpenVideoEditPageResponse(false));
        }
    }
}
