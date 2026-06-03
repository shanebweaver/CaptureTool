using CaptureTool.Application.Abstractions.Audio;

namespace CaptureTool.Application.Abstractions.Features.CaptureOverlay.GetAudioInputSources;

public sealed record GetAudioInputSourcesResponse(IReadOnlyList<AudioInputSource> Sources);
