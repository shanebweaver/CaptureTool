using CaptureTool.Application.Abstractions.Edit.Audio.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Edit.Image.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Edit.Video.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.OpenRecentCapture;
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
    private readonly IStorageService _storageService;
    private readonly IRecentCaptureCatalog _recentCaptureCatalog;
    private readonly IOpenAudioEditPageUseCase _goToAudioEdit;
    private readonly IOpenImageEditPageUseCase _goToImageEdit;
    private readonly IOpenVideoEditPageUseCase _goToVideoEdit;

    public OpenRecentCaptureUseCase(
        IFileSystem fileSystem,
        IStorageService storageService,
        IRecentCaptureCatalog recentCaptureCatalog,
        IOpenAudioEditPageUseCase goToAudioEdit,
        IOpenImageEditPageUseCase goToImageEdit,
        IOpenVideoEditPageUseCase goToVideoEdit,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _fileSystem = fileSystem;
        _storageService = storageService;
        _recentCaptureCatalog = recentCaptureCatalog;
        _goToAudioEdit = goToAudioEdit;
        _goToImageEdit = goToImageEdit;
        _goToVideoEdit = goToVideoEdit;
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

                string workingFilePath = PrepareWorkingFile(request.FilePath);
                switch (fileType)
                {
                    case CaptureFileType.Audio:
                        await _goToAudioEdit.ExecuteAsync(new OpenAudioEditPageRequest(new AudioFile(workingFilePath)), cancellationToken);
                        break;

                    case CaptureFileType.Image:
                        await _goToImageEdit.ExecuteAsync(
                            new OpenImageEditPageRequest(new ImageFile(
                                workingFilePath,
                                string.Equals(workingFilePath, request.FilePath, StringComparison.OrdinalIgnoreCase)
                                    ? null
                                    : request.FilePath)),
                            cancellationToken);
                        break;

                    case CaptureFileType.Video:
                        await _goToVideoEdit.ExecuteAsync(new OpenVideoEditPageRequest(new VideoFile(workingFilePath)), cancellationToken);
                        break;

                    default:
                        return new OpenRecentCaptureResponse(false);
                }

                _recentCaptureCatalog.Touch(request.FilePath);
                return new OpenRecentCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }

    private string PrepareWorkingFile(string sourcePath)
    {
        string temporaryFolderPath = _storageService.GetApplicationTemporaryFolderPath();
        if (IsFileInFolder(sourcePath, temporaryFolderPath))
        {
            return sourcePath;
        }

        _fileSystem.CreateDirectory(temporaryFolderPath);
        string workingFilePath = Path.Combine(
            temporaryFolderPath,
            $"{Path.GetFileNameWithoutExtension(_storageService.GetTemporaryFileName())}{Path.GetExtension(sourcePath)}");
        _fileSystem.CopyFile(sourcePath, workingFilePath, true);
        return workingFilePath;
    }

    private static bool IsFileInFolder(string sourcePath, string folderPath)
    {
        string fullFolderPath = Path.GetFullPath(folderPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullSourcePath = Path.GetFullPath(sourcePath);

        return fullSourcePath.StartsWith(fullFolderPath, StringComparison.OrdinalIgnoreCase);
    }
}
