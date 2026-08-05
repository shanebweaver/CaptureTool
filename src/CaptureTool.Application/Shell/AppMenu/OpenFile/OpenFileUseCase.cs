using CaptureTool.Application.Abstractions.Files;
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
    private readonly IStorageService _storageService;
    private readonly IFileSystem _fileSystem;
    private readonly IRecentCaptureCatalog _recentCaptureCatalog;

    public OpenFileUseCase(
        IFilePickerService filePickerService,
        INavigationCoordinator navigationCoordinator,
        IStorageService storageService,
        IFileSystem fileSystem,
        IRecentCaptureCatalog recentCaptureCatalog,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _filePickerService = filePickerService;
        _navigationCoordinator = navigationCoordinator;
        _storageService = storageService;
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

                        string temporaryFolderPath = _storageService.GetApplicationTemporaryFolderPath();
                        bool isTemporaryFile = IsFileInFolder(file.FilePath, temporaryFolderPath);
                        string filePath = isTemporaryFile
                            ? file.FilePath
                            : CopyFileToFolder(file.FilePath, temporaryFolderPath);
                        bool navigated = fileType switch
                        {
                            CaptureFileType.Audio => await _navigationCoordinator.NavigateAsync(
                                NavigationRoute.AudioEdit,
                                new AudioFile(filePath),
                                cancellationToken: token),
                            CaptureFileType.Image => await _navigationCoordinator.NavigateAsync(
                                NavigationRoute.ImageEdit,
                                new ImageFile(filePath, isTemporaryFile ? null : file.FilePath),
                                cancellationToken: token),
                            CaptureFileType.Video => await _navigationCoordinator.NavigateAsync(
                                NavigationRoute.VideoEdit,
                                new VideoFile(filePath),
                                cancellationToken: token),
                            _ => false
                        };

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

    private static bool IsFileInFolder(string sourcePath, string folderPath)
    {
        string fullFolderPath = Path.GetFullPath(folderPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullSourcePath = Path.GetFullPath(sourcePath);

        return fullSourcePath.StartsWith(fullFolderPath, StringComparison.OrdinalIgnoreCase);
    }

    private string CopyFileToFolder(string sourcePath, string folderPath)
    {
        _fileSystem.CreateDirectory(folderPath);

        string destinationPath = Path.Combine(
            folderPath,
            $"{Path.GetFileNameWithoutExtension(_storageService.GetTemporaryFileName())}{Path.GetExtension(sourcePath)}");

        _fileSystem.CopyFile(sourcePath, destinationPath, true);
        return destinationPath;
    }
}
