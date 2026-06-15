using CaptureTool.Application.Abstractions.Features.AudioEdit.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Features.ImageEdit.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Features.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.Features.VideoEdit.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Features.RecentCaptures.OpenRecentCapture;

public sealed class OpenRecentCaptureUseCase : IOpenRecentCaptureUseCase
{
    private readonly IFileTypeDetector _fileTypeDetector;
    private readonly IOpenAudioEditPageUseCase _goToAudioEdit;
    private readonly IOpenImageEditPageUseCase _goToImageEdit;
    private readonly IOpenVideoEditPageUseCase _goToVideoEdit;

    public OpenRecentCaptureUseCase(
        IFileTypeDetector fileTypeDetector,
        IOpenAudioEditPageUseCase goToAudioEdit,
        IOpenImageEditPageUseCase goToImageEdit,
        IOpenVideoEditPageUseCase goToVideoEdit)
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
        try
        {
            if (!File.Exists(request.FilePath))
            {
                return new OpenRecentCaptureResponse(false);
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
                    return new OpenRecentCaptureResponse(false);
            }

            return new OpenRecentCaptureResponse();
        }
        catch (Exception)
        {
            return new OpenRecentCaptureResponse(false);
        }
    }
}
