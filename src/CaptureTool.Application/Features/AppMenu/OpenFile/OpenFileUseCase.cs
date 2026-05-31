using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.AudioEdit.OpenAudioEditPage;
using CaptureTool.Application.Features.ImageEdit.OpenImageEditPage;
using CaptureTool.Application.Features.VideoEdit.OpenVideoEditPage;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Windowing;

namespace CaptureTool.Application.Features.AppMenu.OpenFile;

public sealed class OpenFileUseCase : IUseCase<OpenFileRequest, OpenFileResponse>
{
    private readonly IFilePickerService _filePickerService;
    private readonly IFileTypeDetector _fileTypeDetector;
    private readonly IUseCase<OpenAudioEditPageRequest, OpenAudioEditPageResponse> _goToAudioEdit;
    private readonly IUseCase<OpenImageEditPageRequest, OpenImageEditPageResponse> _goToImageEdit;
    private readonly IUseCase<OpenVideoEditPageRequest, OpenVideoEditPageResponse> _goToVideoEdit;
    private readonly IWindowHandleProvider _windowHandleProvider;

    public OpenFileUseCase(
        IFilePickerService filePickerService,
        IFileTypeDetector fileTypeDetector,
        IUseCase<OpenAudioEditPageRequest, OpenAudioEditPageResponse> goToAudioEdit,
        IUseCase<OpenImageEditPageRequest, OpenImageEditPageResponse> goToImageEdit,
        IUseCase<OpenVideoEditPageRequest, OpenVideoEditPageResponse> goToVideoEdit,
        IWindowHandleProvider windowHandleProvider)
    {
        _filePickerService = filePickerService;
        _fileTypeDetector = fileTypeDetector;
        _goToAudioEdit = goToAudioEdit;
        _goToImageEdit = goToImageEdit;
        _goToVideoEdit = goToVideoEdit;
        _windowHandleProvider = windowHandleProvider;
    }

    public async Task<OpenFileResponse> ExecuteAsync(OpenFileRequest request, CancellationToken cancellationToken = default)
    {
        nint hwnd = _windowHandleProvider.GetMainWindowHandle();
        IFile file = await _filePickerService.PickFileAsync(hwnd, FilePickerType.AnyCapture, UserFolder.Videos)
            ?? throw new OperationCanceledException("No file was selected.");
        cancellationToken.ThrowIfCancellationRequested();

        switch (_fileTypeDetector.DetectFileType(file.FilePath))
        {
            case CaptureFileType.Audio:
                await _goToAudioEdit.ExecuteAsync(new OpenAudioEditPageRequest(new AudioFile(file.FilePath)), cancellationToken);
                break;

            case CaptureFileType.Image:
                await _goToImageEdit.ExecuteAsync(new OpenImageEditPageRequest(new ImageFile(file.FilePath)), cancellationToken);
                break;

            case CaptureFileType.Video:
                await _goToVideoEdit.ExecuteAsync(new OpenVideoEditPageRequest(new VideoFile(file.FilePath)), cancellationToken);
                break;

            default:
                throw new InvalidOperationException($"Unknown file type: {file.FilePath}");
        }

        return new OpenFileResponse();
    }
}
