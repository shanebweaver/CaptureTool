using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.Features.AudioEdit.OpenAudioEditPage;

public sealed record OpenAudioEditPageRequest(IAudioFile AudioFile);
