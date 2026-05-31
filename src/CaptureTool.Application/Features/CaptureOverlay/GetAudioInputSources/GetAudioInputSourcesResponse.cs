using CaptureTool.Infrastructure.Abstractions.Audio;

namespace CaptureTool.Application.Features.CaptureOverlay.GetAudioInputSources;

public sealed record GetAudioInputSourcesResponse(IReadOnlyList<AudioInputSource> Sources);
