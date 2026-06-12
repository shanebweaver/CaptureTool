using CaptureTool.Application.Abstractions.Features.AppMenu.OpenFile;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Application.Features.AppMenu.OpenFile;

public sealed class OpenFileUseCase : IOpenFileUseCase
{
    private readonly IFileTypeDetector _fileTypeDetector;
    private readonly IFilePickerService _filePickerService;
    private readonly INavigationService _navigationService;
    private readonly IStorageService _storageService;
    private readonly IWindowHandleProvider _windowHandleProvider;

    public OpenFileUseCase(
        IFileTypeDetector fileTypeDetector,
        IFilePickerService filePickerService,
        INavigationService navigationService,
        IStorageService storageService,
        IWindowHandleProvider windowHandleProvider)
    {
        _fileTypeDetector = fileTypeDetector;
        _filePickerService = filePickerService;
        _navigationService = navigationService;
        _storageService = storageService;
        _windowHandleProvider = windowHandleProvider;
    }

    public async Task<OpenFileResponse> ExecuteAsync(OpenFileRequest request, CancellationToken cancellationToken = default)
    {
        nint hwnd = _windowHandleProvider.GetMainWindowHandle();
        IFile file = await _filePickerService.PickFileAsync(hwnd, FilePickerType.ImageOrVideo, UserFolder.Pictures)
            ?? throw new OperationCanceledException("No file was selected.");
        cancellationToken.ThrowIfCancellationRequested();

        string filePath = CopyFileToTemporaryFolder(file.FilePath);
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
                throw new InvalidOperationException($"Unknown file type: {fileType}");
        }

        return new OpenFileResponse();
    }

    private string CopyFileToTemporaryFolder(string sourcePath)
    {
        string temporaryFolderPath = _storageService.GetApplicationTemporaryFolderPath();
        Directory.CreateDirectory(temporaryFolderPath);

        string destinationPath = Path.Combine(
            temporaryFolderPath,
            $"{Path.GetFileNameWithoutExtension(_storageService.GetTemporaryFileName())}{Path.GetExtension(sourcePath)}");

        File.Copy(sourcePath, destinationPath, true);
        return destinationPath;
    }
}
