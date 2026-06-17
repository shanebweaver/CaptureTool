using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.SelectAudioInputSource;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.SelectAudioInputSource;

public sealed class SelectAudioInputSourceUseCase : ISelectAudioInputSourceUseCase
{
    private const string ActivityId = "SelectAudioInputSource";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioInputDetectionService _audioInputDetectionService;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public SelectAudioInputSourceUseCase(IAudioInputDetectionService audioInputDetectionService,
        IVideoCaptureHandler videoCaptureHandler,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioInputDetectionService = audioInputDetectionService;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public Task<UseCaseResponse<SelectAudioInputSourceResponse>> ExecuteAsync(SelectAudioInputSourceRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (string.IsNullOrWhiteSpace(request.SourceId))
                {
                    return new SelectAudioInputSourceResponse(false, false);
                }

                IReadOnlyList<AudioInputSource> sources = await _audioInputDetectionService.GetAudioInputSourcesAsync(cancellationToken);
                bool isAvailable = sources.Any(source => string.Equals(source.Id, request.SourceId, StringComparison.OrdinalIgnoreCase));
                if (isAvailable)
                {
                    _videoCaptureHandler.SelectAudioInputSource(request.SourceId);
                }

                return new SelectAudioInputSourceResponse(isAvailable, !isAvailable);
            },
            cancellationToken: cancellationToken);
    }
}
