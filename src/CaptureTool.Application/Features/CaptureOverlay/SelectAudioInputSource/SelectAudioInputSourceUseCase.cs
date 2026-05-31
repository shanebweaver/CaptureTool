using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Audio;

namespace CaptureTool.Application.Features.CaptureOverlay.SelectAudioInputSource;

public sealed class SelectAudioInputSourceUseCase : IUseCase<SelectAudioInputSourceRequest, SelectAudioInputSourceResponse>
{
    private readonly IAudioInputDetectionService _audioInputDetectionService;

    public SelectAudioInputSourceUseCase(IAudioInputDetectionService audioInputDetectionService)
    {
        _audioInputDetectionService = audioInputDetectionService;
    }

    public async Task<SelectAudioInputSourceResponse> ExecuteAsync(SelectAudioInputSourceRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SourceId))
        {
            return new SelectAudioInputSourceResponse(false, false);
        }

        IReadOnlyList<AudioInputSource> sources = await _audioInputDetectionService.GetAudioInputSourcesAsync(cancellationToken);
        bool isAvailable = sources.Any(source => string.Equals(source.Id, request.SourceId, StringComparison.OrdinalIgnoreCase));

        return new SelectAudioInputSourceResponse(isAvailable, !isAvailable);
    }
}
