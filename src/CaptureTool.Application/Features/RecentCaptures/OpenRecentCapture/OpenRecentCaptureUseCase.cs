using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.AudioEdit.OpenAudioEditPage;
using CaptureTool.Application.Features.ImageEdit.OpenImageEditPage;
using CaptureTool.Application.Features.VideoEdit.OpenVideoEditPage;
using CaptureTool.Domain.Capture.Abstractions;

namespace CaptureTool.Application.Features.RecentCaptures.OpenRecentCapture;

public sealed class OpenRecentCaptureUseCase : IUseCase<OpenRecentCaptureRequest, OpenRecentCaptureResponse>, IConditional<OpenRecentCaptureRequest>
{
    private readonly IFileTypeDetector _fileTypeDetector;
    private readonly IUseCase<OpenAudioEditPageRequest, OpenAudioEditPageResponse> _goToAudioEdit;
    private readonly IUseCase<OpenImageEditPageRequest, OpenImageEditPageResponse> _goToImageEdit;
    private readonly IUseCase<OpenVideoEditPageRequest, OpenVideoEditPageResponse> _goToVideoEdit;

    public OpenRecentCaptureUseCase(
        IFileTypeDetector fileTypeDetector,
        IUseCase<OpenAudioEditPageRequest, OpenAudioEditPageResponse> goToAudioEdit,
        IUseCase<OpenImageEditPageRequest, OpenImageEditPageResponse> goToImageEdit,
        IUseCase<OpenVideoEditPageRequest, OpenVideoEditPageResponse> goToVideoEdit)
    {
        _fileTypeDetector = fileTypeDetector;
        _goToAudioEdit = goToAudioEdit;
        _goToImageEdit = goToImageEdit;
        _goToVideoEdit = goToVideoEdit;
    }

    public Task<bool> CanExecuteAsync(OpenRecentCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(request.FilePath));
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