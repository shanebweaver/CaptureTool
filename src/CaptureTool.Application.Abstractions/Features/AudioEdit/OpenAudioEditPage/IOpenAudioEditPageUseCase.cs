using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.AudioEdit.OpenAudioEditPage;

public interface IOpenAudioEditPageUseCase : IUseCase<OpenAudioEditPageRequest, OpenAudioEditPageResponse>, IConditional<OpenAudioEditPageRequest>
{
}