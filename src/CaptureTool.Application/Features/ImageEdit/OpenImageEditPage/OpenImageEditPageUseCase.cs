using CaptureTool.Application.Abstractions.Features.ImageEdit.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.ImageEdit.OpenImageEditPage;

public sealed class OpenImageEditPageUseCase : IOpenImageEditPageUseCase
{
    private const string ActivityId = "OpenImageEditPage";

    private readonly INavigationService _navigationService;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public OpenImageEditPageUseCase(
        INavigationService navigationService,
        IUseCaseExecutor useCaseExecutor)
    {
        _navigationService = navigationService;
        _useCaseExecutor = useCaseExecutor;
    }

    public bool CanExecute(OpenImageEditPageRequest request)
    {
        return true;
    }

    public Task<UseCaseResponse<OpenImageEditPageResponse>> ExecuteAsync(OpenImageEditPageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _navigationService.Navigate(NavigationRoute.ImageEdit, request.ImageFile);
                return new OpenImageEditPageResponse();
            },
            cancellationToken: cancellationToken);
    }
}
