using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.VideoEdit.OpenVideoEditPage;

public sealed class OpenVideoEditPageUseCase : IUseCase<OpenVideoEditPageRequest, OpenVideoEditPageResponse>
{
    private readonly INavigationService _navigationService;

    public OpenVideoEditPageUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<OpenVideoEditPageResponse> ExecuteAsync(OpenVideoEditPageRequest request, CancellationToken cancellationToken = default)
    {
        _navigationService.Navigate(NavigationRoute.VideoEdit, request.VideoFile);
        return Task.FromResult(new OpenVideoEditPageResponse());
    }
}