using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.Features.ImageEdit.OpenImageEditPage;

public sealed class OpenImageEditPageUseCase : IUseCase<OpenImageEditPageRequest, OpenImageEditPageResponse>, IConditional<OpenImageEditPageRequest>
{
    private readonly INavigationService _navigationService;

    public OpenImageEditPageUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public Task<bool> CanExecuteAsync(OpenImageEditPageRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<OpenImageEditPageResponse> ExecuteAsync(OpenImageEditPageRequest request, CancellationToken cancellationToken = default)
    {
        _navigationService.Navigate(NavigationRoute.ImageEdit, request.ImageFile);
        return Task.FromResult(new OpenImageEditPageResponse());
    }
}