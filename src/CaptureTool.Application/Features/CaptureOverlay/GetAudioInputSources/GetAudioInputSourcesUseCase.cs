using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.GetAudioInputSources;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.GetAudioInputSources;

public sealed class GetAudioInputSourcesUseCase : IGetAudioInputSourcesUseCase
{
    private const string ActivityId = "GetAudioInputSources";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioInputDetectionService _audioInputDetectionService;

    public GetAudioInputSourcesUseCase(IAudioInputDetectionService audioInputDetectionService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioInputDetectionService = audioInputDetectionService;
    }

    public Task<UseCaseResponse<GetAudioInputSourcesResponse>> ExecuteAsync(GetAudioInputSourcesRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                IReadOnlyList<AudioInputSource> sources = await _audioInputDetectionService.GetAudioInputSourcesAsync(cancellationToken);
                return new GetAudioInputSourcesResponse(sources);
            },
            cancellationToken: cancellationToken);
    }
}
