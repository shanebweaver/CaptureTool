using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.GetAudioInputSources;

namespace CaptureTool.Application.Features.CaptureOverlay.GetAudioInputSources;

public sealed class GetAudioInputSourcesUseCase : IGetAudioInputSourcesUseCase
{
    private readonly IAudioInputDetectionService _audioInputDetectionService;

    public GetAudioInputSourcesUseCase(IAudioInputDetectionService audioInputDetectionService)
    {
        _audioInputDetectionService = audioInputDetectionService;
    }

    public async Task<GetAudioInputSourcesResponse> ExecuteAsync(GetAudioInputSourcesRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AudioInputSource> sources = await _audioInputDetectionService.GetAudioInputSourcesAsync(cancellationToken);
        return new GetAudioInputSourcesResponse(sources);
    }
}
