using CaptureTool.Application.Abstractions.Edit.Image.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Edit.Image.OpenImageEditPage;

internal sealed class OpenImageEditPageUseCase : IOpenImageEditPageUseCase
{
    private const string ActivityId = "OpenImageEditPage";

    private readonly INavigationCoordinator _navigationCoordinator;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public OpenImageEditPageUseCase(
        INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor)
    {
        _navigationCoordinator = navigationCoordinator;
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
            useCase: async _ =>
            {
                bool navigated = await _navigationCoordinator.NavigateAsync(
                    NavigationRoute.ImageEdit,
                    request.ImageFile,
                    cancellationToken: cancellationToken);
                return new OpenImageEditPageResponse(navigated);
            },
            cancellationToken: cancellationToken);
    }
}
