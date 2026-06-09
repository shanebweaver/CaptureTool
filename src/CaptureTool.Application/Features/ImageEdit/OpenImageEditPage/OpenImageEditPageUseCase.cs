using CaptureTool.Application.Abstractions.Features.ImageEdit.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.ImageEdit.OpenImageEditPage;

public sealed class OpenImageEditPageUseCase : IOpenImageEditPageUseCase
{
    private readonly INavigationService _navigationService;

    public OpenImageEditPageUseCase(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public bool CanExecute(OpenImageEditPageRequest request)
    {
        return true;
    }

    public Task<OpenImageEditPageResponse> ExecuteAsync(OpenImageEditPageRequest request, CancellationToken cancellationToken = default)
    {
        _navigationService.Navigate(NavigationRoute.ImageEdit, request.ImageFile);
        return Task.FromResult(new OpenImageEditPageResponse());
    }
}