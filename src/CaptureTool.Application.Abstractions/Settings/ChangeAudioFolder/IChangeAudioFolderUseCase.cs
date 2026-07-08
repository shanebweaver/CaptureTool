using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Settings.ChangeAudioFolder;

public interface IChangeAudioFolderUseCase : IUseCase<ChangeAudioFolderRequest, ChangeAudioFolderResponse>, IConditional<ChangeAudioFolderRequest>
{
}
