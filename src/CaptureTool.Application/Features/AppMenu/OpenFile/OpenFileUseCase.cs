using CaptureTool.Application.Abstractions.Features.AppMenu.OpenFile;
using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.AudioCapture;

namespace CaptureTool.Application.Features.AppMenu.OpenFile;

public sealed class OpenFileUseCase : IOpenFileUseCase
{
    private const string ActivityId = "OpenFile";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFileTypeDetector _fileTypeDetector;
    private readonly IFilePickerService _filePickerService;
    private readonly INavigationService _navigationService;
    private readonly IStorageService _storageService;
    private readonly IAudioCaptureNavigationGuard _audioCaptureNavigationGuard;

    public OpenFileUseCase(IFileTypeDetector fileTypeDetector,
        IFilePickerService filePickerService,
        INavigationService navigationService,
        IStorageService storageService,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureNavigationGuard? audioCaptureNavigationGuard = null)
    {
        _useCaseExecutor = useCaseExecutor;
        _fileTypeDetector = fileTypeDetector;
        _filePickerService = filePickerService;
        _navigationService = navigationService;
        _storageService = storageService;
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

                IFile? file = await _filePickerService.PickFileAsync(FilePickerType.ImageOrVideo, UserFolder.Pictures);
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

                CaptureFileType fileType = _fileTypeDetector.DetectFileType(filePath);
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

    private static string CopyFileToFolder(string sourcePath, string folderPath)
    {
        Directory.CreateDirectory(folderPath);

        string destinationPath = Path.Combine(
            folderPath,
            Path.GetFileName(sourcePath));

        File.Copy(sourcePath, destinationPath, true);
        return destinationPath;
    }

    private static void MarkFileAsRecentlyOpened(string filePath)
    {
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow);
    }
}
