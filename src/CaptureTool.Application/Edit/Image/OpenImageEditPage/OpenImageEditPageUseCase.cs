using CaptureTool.Application.Abstractions.Edit.Image.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Edit;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;
using CaptureTool.Application.Capture.Assets;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Edit.Image.OpenImageEditPage;

internal sealed class OpenImageEditPageUseCase : IOpenImageEditPageUseCase
{
    private const string ActivityId = "OpenImageEditPage";

    private readonly INavigationCoordinator _navigationCoordinator;
    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ICaptureAssetLifecycleService? _captureAssetLifecycleService;

    public OpenImageEditPageUseCase(
        INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor,
        ICaptureAssetLifecycleService? captureAssetLifecycleService = null)
    {
        _navigationCoordinator = navigationCoordinator;
        _useCaseExecutor = useCaseExecutor;
        _captureAssetLifecycleService = captureAssetLifecycleService;
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
                CaptureEditorContext editorContext = request.EditorContext ?? new CaptureEditorContext(
                    request.ImageFile.PersistentFilePath ?? request.ImageFile.FilePath);
                bool navigated = await _navigationCoordinator.NavigateAsync(
                    NavigationRoute.ImageEdit,
                    request with { EditorContext = editorContext },
                    cancellationToken: cancellationToken);
                if (navigated && !editorContext.CaptureId.HasValue)
                {
                    _captureAssetLifecycleService?.TryRegisterOpened(
                        editorContext.PersistentSourcePath,
                        CaptureFileType.Image);
                }

                return new OpenImageEditPageResponse(navigated);
            },
            cancellationToken: cancellationToken);
    }
}
