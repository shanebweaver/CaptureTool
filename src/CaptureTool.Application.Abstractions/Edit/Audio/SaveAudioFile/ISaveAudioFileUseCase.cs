using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Edit.Audio.SaveAudioFile;

public interface ISaveAudioFileUseCase : IUseCase<SaveAudioFileRequest, SaveAudioFileResponse>, IConditional<SaveAudioFileRequest>
{
}