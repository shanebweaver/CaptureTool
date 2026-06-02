using CaptureTool.Domain.Capture.Abstractions.Files;

namespace CaptureTool.Application.Abstractions.Features.AudioEdit.OpenAudioEditPage;

public sealed record OpenAudioEditPageRequest(IAudioFile AudioFile);
