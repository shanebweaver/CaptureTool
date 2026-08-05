using CaptureTool.Application.Abstractions.Edit.Audio.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Edit.Image.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Edit.Video.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Library.RecentCaptures.OpenRecentCapture;

internal sealed class OpenRecentCaptureUseCase : IOpenRecentCaptureUseCase
{
    private const string ActivityId = "OpenRecentCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFileSystem _fileSystem;
    private readonly IScratchArtifactStore _scratchArtifactStore;
    private readonly IRecentCaptureCatalog _recentCaptureCatalog;
    private readonly IOpenAudioEditPageUseCase _goToAudioEdit;
    private readonly IOpenImageEditPageUseCase _goToImageEdit;
    private readonly IOpenVideoEditPageUseCase _goToVideoEdit;
    private readonly INavigationCoordinator _navigationCoordinator;

    public OpenRecentCaptureUseCase(
        IFileSystem fileSystem,
        IScratchArtifactStore scratchArtifactStore,
        IRecentCaptureCatalog recentCaptureCatalog,
        IOpenAudioEditPageUseCase goToAudioEdit,
        IOpenImageEditPageUseCase goToImageEdit,
        IOpenVideoEditPageUseCase goToVideoEdit,
        INavigationCoordinator navigationCoordinator,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _fileSystem = fileSystem;
        _scratchArtifactStore = scratchArtifactStore;
        _recentCaptureCatalog = recentCaptureCatalog;
        _goToAudioEdit = goToAudioEdit;
        _goToImageEdit = goToImageEdit;
        _goToVideoEdit = goToVideoEdit;
        _navigationCoordinator = navigationCoordinator;
    }

    public bool CanExecute(OpenRecentCaptureRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.FilePath);
    }

    public Task<UseCaseResponse<OpenRecentCaptureResponse>> ExecuteAsync(OpenRecentCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (!_fileSystem.FileExists(request.FilePath))
                {
                    return new OpenRecentCaptureResponse(false);
                }

                CaptureFileType fileType = CaptureFileTypeDetector.DetectFileType(request.FilePath);
                if (fileType == CaptureFileType.Unknown)
                {
                    return new OpenRecentCaptureResponse(false);
                }

                bool opened = await _navigationCoordinator.ExecuteTransitionAsync(
                    async token =>
                    {
                        string workingFilePath = PrepareWorkingFile(request.FilePath);
                        bool navigated;
                        try
                        {
                            navigated = fileType switch
                            {
                                CaptureFileType.Audio => (await _goToAudioEdit.ExecuteAsync(
                                    new OpenAudioEditPageRequest(new AudioFile(workingFilePath)),
                                    token)).Value?.Succeeded == true,
                                CaptureFileType.Image => (await _goToImageEdit.ExecuteAsync(
                                    new OpenImageEditPageRequest(new ImageFile(workingFilePath, request.FilePath)),
                                    token)).Value?.Succeeded == true,
                                CaptureFileType.Video => (await _goToVideoEdit.ExecuteAsync(
                                    new OpenVideoEditPageRequest(new VideoFile(workingFilePath)),
                                    token)).Value?.Succeeded == true,
                                _ => false
                            };
                        }
                        catch
                        {
                            _scratchArtifactStore.DeleteArtifact(workingFilePath);
                            throw;
                        }

                        if (!navigated)
                        {
                            _scratchArtifactStore.DeleteArtifact(workingFilePath);
                        }

                        if (navigated)
                        {
                            _recentCaptureCatalog.Touch(request.FilePath);
                        }

                        return navigated;
                    },
                    cancellationToken);

                return new OpenRecentCaptureResponse(opened);
            },
            cancellationToken: cancellationToken);
    }

    private string PrepareWorkingFile(string sourcePath)
    {
        string workingFilePath = _scratchArtifactStore.CreateLeasedArtifactPath("recent-capture-working-copy", Path.GetExtension(sourcePath));
        try
        {
            _fileSystem.CopyFile(sourcePath, workingFilePath, true);
            return workingFilePath;
        }
        catch
        {
            _scratchArtifactStore.DeleteArtifact(workingFilePath);
            throw;
        }
    }
}
