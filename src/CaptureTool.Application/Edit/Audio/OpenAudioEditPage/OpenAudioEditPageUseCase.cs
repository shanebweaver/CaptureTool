using CaptureTool.Application.Abstractions.Edit.Audio.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Edit;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;
using CaptureTool.Application.Capture.Assets;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Edit.Audio.OpenAudioEditPage;

internal sealed class OpenAudioEditPageUseCase : IOpenAudioEditPageUseCase
{
    private const string ActivityId = "OpenAudioEditPage";

    private readonly INavigationCoordinator _navigationCoordinator;
    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFileSystem _fileSystem;
    private readonly ICaptureAssetLifecycleService? _captureAssetLifecycleService;

    public OpenAudioEditPageUseCase(
        INavigationCoordinator navigationCoordinator,
        IFileSystem fileSystem,
        IUseCaseExecutor useCaseExecutor,
        ICaptureAssetLifecycleService? captureAssetLifecycleService = null)
    {
        _navigationCoordinator = navigationCoordinator;
        _fileSystem = fileSystem;
        _useCaseExecutor = useCaseExecutor;
        _captureAssetLifecycleService = captureAssetLifecycleService;
    }

    public bool CanExecute(OpenAudioEditPageRequest request)
    {
        bool canExecute = _fileSystem.FileExists(request.AudioFile.FilePath);
        return canExecute;
    }

    public Task<UseCaseResponse<OpenAudioEditPageResponse>> ExecuteAsync(OpenAudioEditPageRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                CaptureEditorContext editorContext = request.EditorContext ?? new CaptureEditorContext(
                    request.AudioFile.FilePath);
                bool navigated = await _navigationCoordinator.NavigateAsync(
                    NavigationRoute.AudioEdit,
                    request with { EditorContext = editorContext },
                    cancellationToken: cancellationToken);
                if (navigated && !editorContext.CaptureId.HasValue)
                {
                    _captureAssetLifecycleService?.TryRegisterOpened(
                        editorContext.PersistentSourcePath,
                        CaptureFileType.Audio);
                }

                return new OpenAudioEditPageResponse(navigated);
            },
            cancellationToken: cancellationToken);
    }
}
