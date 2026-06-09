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
    private readonly IWindowHandleProvider _windowHandleProvider;

    public OpenFileUseCase(
        IFileTypeDetector fileTypeDetector,
        IFilePickerService filePickerService,
        INavigationService navigationService,
        IWindowHandleProvider windowHandleProvider)
    {
        _fileTypeDetector = fileTypeDetector;
        _filePickerService = filePickerService;
        _navigationService = navigationService;
        _windowHandleProvider = windowHandleProvider;
    }

    public async Task<OpenFileResponse> ExecuteAsync(OpenFileRequest request, CancellationToken cancellationToken = default)
    {
        nint hwnd = _windowHandleProvider.GetMainWindowHandle();
        IFile file = await _filePickerService.PickFileAsync(hwnd, FilePickerType.ImageOrVideo, UserFolder.Pictures)
            ?? throw new OperationCanceledException("No file was selected.");
        cancellationToken.ThrowIfCancellationRequested();

        CaptureFileType fileType = _fileTypeDetector.DetectFileType(file.FilePath);
        switch (fileType)
        {
            case CaptureFileType.Image:
                _navigationService.Navigate(NavigationRoute.ImageEdit, new ImageFile(file.FilePath));
                break;

            case CaptureFileType.Video:
                _navigationService.Navigate(NavigationRoute.VideoEdit, new VideoFile(file.FilePath));
                break;

            default:
                throw new InvalidOperationException($"Unknown file type: {fileType}");
        }

        return new OpenFileResponse();
    }
}
