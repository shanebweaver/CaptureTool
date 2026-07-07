using CaptureTool.Application.Abstractions.Audio;

namespace CaptureTool.Application.Abstractions.Capture.Overlay.GetAudioInputSources;

public sealed record GetAudioInputSourcesResponse(IReadOnlyList<AudioInputSource> Sources);
