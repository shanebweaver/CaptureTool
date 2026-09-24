using CaptureTool.Application.Abstractions.Edit.Video.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.Edit;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;
using CaptureTool.Application.Capture.Assets;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Edit.Video.OpenVideoEditPage;

internal sealed class OpenVideoEditPageUseCase : IOpenVideoEditPageUseCase
{
    private const string ActivityId = "OpenVideoEditPage";

    private readonly INavigationCoordinator _navigationCoordinator;
    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ICaptureAssetLifecycleService? _captureAssetLifecycleService;

    public OpenVideoEditPageUseCase(
        INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor,
        ICaptureAssetLifecycleService? captureAssetLifecycleService = null)
    {
        _navigationCoordinator = navigationCoordinator;
        _useCaseExecutor = useCaseExecutor;
        _captureAssetLifecycleService = captureAssetLifecycleService;
    }

    public Task<UseCaseResponse<OpenVideoEditPageResponse>> ExecuteAsync(OpenVideoEditPageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                CaptureEditorContext editorContext = request.EditorContext ?? new CaptureEditorContext(
                    request.VideoFile.FilePath);
                bool navigated = await _navigationCoordinator.NavigateAsync(
                    NavigationRoute.VideoEdit,
                    request with { EditorContext = editorContext },
                    cancellationToken: cancellationToken);
                if (navigated && !editorContext.CaptureId.HasValue)
                {
                    _captureAssetLifecycleService?.TryRegisterOpened(
                        editorContext.PersistentSourcePath,
                        CaptureFileType.Video);
                }

                return new OpenVideoEditPageResponse(navigated);
            },
            cancellationToken: cancellationToken);
    }
}
