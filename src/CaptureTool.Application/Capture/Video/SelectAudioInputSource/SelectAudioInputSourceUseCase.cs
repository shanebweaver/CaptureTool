using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Capture.Video.SelectAudioInputSource;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Capture.Video;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Video.SelectAudioInputSource;

internal sealed class SelectAudioInputSourceUseCase : ISelectAudioInputSourceUseCase
{
    private const string ActivityId = "SelectAudioInputSource";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioInputDetectionService _audioInputDetectionService;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;

    public SelectAudioInputSourceUseCase(IAudioInputDetectionService audioInputDetectionService,
        IVideoCaptureWorkflow videoCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioInputDetectionService = audioInputDetectionService;
        _videoCaptureWorkflow = videoCaptureWorkflow;
    }

    public Task<UseCaseResponse<SelectAudioInputSourceResponse>> ExecuteAsync(SelectAudioInputSourceRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                if (string.IsNullOrWhiteSpace(request.SourceId))
                {
                    _videoCaptureWorkflow.SelectAudioInputSource(null);
                    return new SelectAudioInputSourceResponse(false, false);
                }

                IReadOnlyList<AudioInputSource> sources = await _audioInputDetectionService.GetAudioInputSourcesAsync(cancellationToken);
                bool isAvailable = sources.Any(source => string.Equals(source.Id, request.SourceId, StringComparison.OrdinalIgnoreCase));
                if (isAvailable)
                {
                    _videoCaptureWorkflow.SelectAudioInputSource(request.SourceId);
                }

                return new SelectAudioInputSourceResponse(isAvailable, !isAvailable);
            },
            cancellationToken: cancellationToken);
    }
}
