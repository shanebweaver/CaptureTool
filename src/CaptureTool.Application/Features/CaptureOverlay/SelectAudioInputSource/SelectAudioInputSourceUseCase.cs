using CaptureTool.Application.Abstractions.Audio;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.SelectAudioInputSource;

namespace CaptureTool.Application.Features.CaptureOverlay.SelectAudioInputSource;

public sealed class SelectAudioInputSourceUseCase : ISelectAudioInputSourceUseCase
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

        IReadOnlyList<AudioInputSource> sources;
        try
        {
            sources = await _audioInputDetectionService.GetAudioInputSourcesAsync(cancellationToken);
        }
        catch (Exception)
        {
            return new SelectAudioInputSourceResponse(false, false);
        }

        bool isAvailable = sources.Any(source => string.Equals(source.Id, request.SourceId, StringComparison.OrdinalIgnoreCase));

        return new SelectAudioInputSourceResponse(isAvailable, !isAvailable);
    }
}
