using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Application.Abstractions.Features.AudioEdit.OpenAudioEditPage;

public sealed record OpenAudioEditPageRequest(IAudioFile AudioFile);
