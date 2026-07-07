using CaptureTool.Application.Abstractions.Features.AppMenu.OpenFile;
using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;
using CaptureTool.Application.Features.AudioCapture;

namespace CaptureTool.Application.Features.AppMenu.OpenFile;

public sealed class OpenFileUseCase : IOpenFileUseCase
{
    private const string ActivityId = "OpenFile";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFilePickerService _filePickerService;
    private readonly INavigationService _navigationService;
    private readonly IStorageService _storageService;
    private readonly IFileSystem _fileSystem;
    private readonly IClock _clock;
    private readonly IAudioCaptureNavigationGuard _audioCaptureNavigationGuard;

    public OpenFileUseCase(
        IFilePickerService filePickerService,
        INavigationService navigationService,
        IStorageService storageService,
        IFileSystem fileSystem,
        IClock clock,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureNavigationGuard? audioCaptureNavigationGuard = null)
    {
        _useCaseExecutor = useCaseExecutor;
        _filePickerService = filePickerService;
        _navigationService = navigationService;
        _storageService = storageService;
        _fileSystem = fileSystem;
        _clock = clock;
        _audioCaptureNavigationGuard = audioCaptureNavigationGuard ?? new AllowAudioCaptureNavigationGuard();
    }

    public Task<UseCaseResponse<OpenFileResponse>> ExecuteAsync(OpenFileRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (!await _audioCaptureNavigationGuard.CanNavigateAwayFromActiveCaptureAsync(cancellationToken))
                {
                    return new OpenFileResponse(false);
                }

                FileReference? file = await _filePickerService.PickFileAsync(FilePickerType.ImageOrVideo, UserFolder.Pictures);
                if (file is null)
                {
                    return new OpenFileResponse(false);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return new OpenFileResponse(false);
                }

                string temporaryFolderPath = _storageService.GetApplicationTemporaryFolderPath();
                string filePath = IsFileInFolder(file.FilePath, temporaryFolderPath)
                ? file.FilePath
                : CopyFileToFolder(file.FilePath, temporaryFolderPath);
                MarkFileAsRecentlyOpened(filePath);

                CaptureFileType fileType = CaptureFileTypeDetector.DetectFileType(filePath);
                switch (fileType)
                {
                    case CaptureFileType.Image:
                        _navigationService.Navigate(NavigationRoute.ImageEdit, new ImageFile(filePath));
                        break;

                    case CaptureFileType.Video:
                        _navigationService.Navigate(NavigationRoute.VideoEdit, new VideoFile(filePath));
                        break;

                    default:
                        return new OpenFileResponse(false);
                }

                return new OpenFileResponse();
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
            Path.GetFileName(sourcePath));

        _fileSystem.CopyFile(sourcePath, destinationPath, true);
        return destinationPath;
    }

    private void MarkFileAsRecentlyOpened(string filePath)
    {
        _fileSystem.SetLastWriteTimeUtc(filePath, _clock.UtcNow);
    }
}
