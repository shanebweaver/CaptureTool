using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Edit;
using CaptureTool.Application.Abstractions.Edit.Audio.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Edit.Image.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Edit.Video.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Shell.AppMenu.OpenFile;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Shell.AppMenu.OpenFile;

internal sealed class OpenFileUseCase : IOpenFileUseCase
{
    private const string ActivityId = "OpenFile";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFilePickerService _filePickerService;
    private readonly INavigationCoordinator _navigationCoordinator;
    private readonly IScratchArtifactStore _scratchArtifactStore;
    private readonly IFileSystem _fileSystem;
    private readonly IRecentCaptureCatalog _recentCaptureCatalog;

    public OpenFileUseCase(
        IFilePickerService filePickerService,
        INavigationCoordinator navigationCoordinator,
        IScratchArtifactStore scratchArtifactStore,
        IFileSystem fileSystem,
        IRecentCaptureCatalog recentCaptureCatalog,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _filePickerService = filePickerService;
        _navigationCoordinator = navigationCoordinator;
        _scratchArtifactStore = scratchArtifactStore;
        _fileSystem = fileSystem;
        _recentCaptureCatalog = recentCaptureCatalog;
    }

    public Task<UseCaseResponse<OpenFileResponse>> ExecuteAsync(OpenFileRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                bool opened = await _navigationCoordinator.ExecuteTransitionAsync(
                    async token =>
                    {
                        FileReference? file = await _filePickerService.PickFileAsync(
                            FilePickerType.CaptureMedia,
                            UserFolder.Pictures);
                        if (file is null || token.IsCancellationRequested)
                        {
                            return false;
                        }

                        CaptureFileType fileType = CaptureFileTypeDetector.DetectFileType(file.FilePath);
                        if (fileType == CaptureFileType.Unknown)
                        {
                            return false;
                        }

                        string workingFilePath = CopyFileToScratch(file.FilePath);
                        bool navigated;
                        try
                        {
                            navigated = fileType switch
                            {
                                CaptureFileType.Audio => await _navigationCoordinator.NavigateAsync(
                                    NavigationRoute.AudioEdit,
                                    new OpenAudioEditPageRequest(
                                        new AudioFile(workingFilePath),
                                        new CaptureEditorContext(file.FilePath)),
                                    cancellationToken: token),
                                CaptureFileType.Image => await _navigationCoordinator.NavigateAsync(
                                    NavigationRoute.ImageEdit,
                                    new OpenImageEditPageRequest(
                                        new ImageFile(workingFilePath, file.FilePath),
                                        new CaptureEditorContext(file.FilePath)),
                                    cancellationToken: token),
                                CaptureFileType.Video => await _navigationCoordinator.NavigateAsync(
                                    NavigationRoute.VideoEdit,
                                    new OpenVideoEditPageRequest(
                                        new VideoFile(workingFilePath),
                                        new CaptureEditorContext(file.FilePath)),
                                    cancellationToken: token),
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
                            _recentCaptureCatalog.RecordOpened(file.FilePath, fileType);
                        }

                        return navigated;
                    },
                    cancellationToken);

                return new OpenFileResponse(opened);
            },
            cancellationToken: cancellationToken);
    }

    private string CopyFileToScratch(string sourcePath)
    {
        string artifactPath = _scratchArtifactStore.CreateLeasedArtifactPath("imported-working-copy", Path.GetExtension(sourcePath));
        try
        {
            _fileSystem.CopyFile(sourcePath, artifactPath, true);
            return artifactPath;
        }
        catch
        {
            _scratchArtifactStore.DeleteArtifact(artifactPath);
            throw;
        }
    }
}
