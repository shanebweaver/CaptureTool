using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.AudioEdit.OpenAudioEditPage;
using CaptureTool.Application.Features.ImageEdit.OpenImageEditPage;
using CaptureTool.Application.Features.VideoEdit.OpenVideoEditPage;
using CaptureTool.Domain.Capture.Abstractions;

namespace CaptureTool.Application.Features.RecentCaptures.OpenRecentCapture;

public sealed class OpenRecentCaptureUseCase : IUseCase<OpenRecentCaptureRequest, OpenRecentCaptureResponse>, IConditional<OpenRecentCaptureRequest>
{
    private readonly IFileTypeDetector _fileTypeDetector;
    private readonly OpenAudioEditPageUseCase _goToAudioEdit;
    private readonly OpenImageEditPageUseCase _goToImageEdit;
    private readonly OpenVideoEditPageUseCase _goToVideoEdit;

    public OpenRecentCaptureUseCase(
        IFileTypeDetector fileTypeDetector,
        OpenAudioEditPageUseCase goToAudioEdit,
        OpenImageEditPageUseCase goToImageEdit,
        OpenVideoEditPageUseCase goToVideoEdit)
    {
        _fileTypeDetector = fileTypeDetector;
        _goToAudioEdit = goToAudioEdit;
        _goToImageEdit = goToImageEdit;
        _goToVideoEdit = goToVideoEdit;
    }

    public bool CanExecute(OpenRecentCaptureRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.FilePath);
    }

    public async Task<OpenRecentCaptureResponse> ExecuteAsync(OpenRecentCaptureRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.FilePath))
        {
            throw new FileNotFoundException($"File not found: {request.FilePath}");
        }

        var fileType = _fileTypeDetector.DetectFileType(request.FilePath);
        switch (fileType)
        {
            case CaptureFileType.Audio:
                await _goToAudioEdit.ExecuteAsync(new OpenAudioEditPageRequest(new AudioFile(request.FilePath)), cancellationToken);
                break;

            case CaptureFileType.Image:
                await _goToImageEdit.ExecuteAsync(new OpenImageEditPageRequest(new ImageFile(request.FilePath)), cancellationToken);
                break;

            case CaptureFileType.Video:
                await _goToVideoEdit.ExecuteAsync(new OpenVideoEditPageRequest(new VideoFile(request.FilePath)), cancellationToken);
                break;

            default:
                throw new InvalidOperationException($"Unknown file type: {fileType}");
        }
        return new OpenRecentCaptureResponse();
    }
}
