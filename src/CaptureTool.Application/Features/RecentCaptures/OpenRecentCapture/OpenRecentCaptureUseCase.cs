using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioEdit.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Features.ImageEdit.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Features.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.Features.VideoEdit.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Domain.Capture;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.RecentCaptures.OpenRecentCapture;

public sealed class OpenRecentCaptureUseCase : IOpenRecentCaptureUseCase
{
    private const string ActivityId = "OpenRecentCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFileTypeDetector _fileTypeDetector;
    private readonly IOpenAudioEditPageUseCase _goToAudioEdit;
    private readonly IOpenImageEditPageUseCase _goToImageEdit;
    private readonly IOpenVideoEditPageUseCase _goToVideoEdit;
    private readonly IAudioCaptureFeatureAvailability? _audioCaptureFeatureAvailability;

    public OpenRecentCaptureUseCase(IFileTypeDetector fileTypeDetector,
        IOpenAudioEditPageUseCase goToAudioEdit,
        IOpenImageEditPageUseCase goToImageEdit,
        IOpenVideoEditPageUseCase goToVideoEdit,
        IUseCaseExecutor useCaseExecutor,
        IAudioCaptureFeatureAvailability? audioCaptureFeatureAvailability = null)
    {
        _useCaseExecutor = useCaseExecutor;
        _fileTypeDetector = fileTypeDetector;
        _goToAudioEdit = goToAudioEdit;
        _goToImageEdit = goToImageEdit;
        _goToVideoEdit = goToVideoEdit;
        _audioCaptureFeatureAvailability = audioCaptureFeatureAvailability;
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
                if (!File.Exists(request.FilePath))
                {
                    return new OpenRecentCaptureResponse(false);
                }

                var fileType = _fileTypeDetector.DetectFileType(request.FilePath);
                switch (fileType)
                {
                    case CaptureFileType.Audio:
                        if (_audioCaptureFeatureAvailability?.IsAudioCaptureEnabled == false)
                        {
                            return new OpenRecentCaptureResponse(false);
                        }

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
            },
            cancellationToken: cancellationToken);
    }
}
