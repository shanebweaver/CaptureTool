using CaptureTool.Application.Abstractions.Edit.Audio.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Edit.Image.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Edit.Video.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Library.RecentCaptures.OpenRecentCapture;

internal sealed class OpenRecentCaptureUseCase : IOpenRecentCaptureUseCase
{
    private const string ActivityId = "OpenRecentCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFileSystem _fileSystem;
    private readonly IOpenAudioEditPageUseCase _goToAudioEdit;
    private readonly IOpenImageEditPageUseCase _goToImageEdit;
    private readonly IOpenVideoEditPageUseCase _goToVideoEdit;

    public OpenRecentCaptureUseCase(
        IFileSystem fileSystem,
        IOpenAudioEditPageUseCase goToAudioEdit,
        IOpenImageEditPageUseCase goToImageEdit,
        IOpenVideoEditPageUseCase goToVideoEdit,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _fileSystem = fileSystem;
        _goToAudioEdit = goToAudioEdit;
        _goToImageEdit = goToImageEdit;
        _goToVideoEdit = goToVideoEdit;
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
                if (!_fileSystem.FileExists(request.FilePath))
                {
                    return new OpenRecentCaptureResponse(false);
                }

                var fileType = CaptureFileTypeDetector.DetectFileType(request.FilePath);
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
            },
            cancellationToken: cancellationToken);
    }
}
