using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Edit.Audio.OpenAudioEditPage;

public interface IOpenAudioEditPageUseCase : IUseCase<OpenAudioEditPageRequest, OpenAudioEditPageResponse>, IConditional<OpenAudioEditPageRequest>
{
}