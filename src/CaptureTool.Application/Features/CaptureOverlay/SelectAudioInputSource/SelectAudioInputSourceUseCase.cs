using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.SelectAudioInputSource;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.SelectAudioInputSource;

public sealed class SelectAudioInputSourceUseCase : ISelectAudioInputSourceUseCase
{
    private const string ActivityId = "SelectAudioInputSource";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioInputDetectionService _audioInputDetectionService;

    public SelectAudioInputSourceUseCase(IAudioInputDetectionService audioInputDetectionService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioInputDetectionService = audioInputDetectionService;
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

                return new SelectAudioInputSourceResponse(isAvailable, !isAvailable);
            },
            cancellationToken: cancellationToken);
    }
}
