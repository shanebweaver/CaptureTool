using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Settings.OpenAudioFolder;

public interface IOpenAudioFolderUseCase : IUseCase<OpenAudioFolderRequest, OpenAudioFolderResponse>, IConditional<OpenAudioFolderRequest>
{
}
