using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.AudioEdit.SaveAudioFile;

public interface ISaveAudioFileUseCase : IUseCase<SaveAudioFileRequest, SaveAudioFileResponse>, IConditional<SaveAudioFileRequest>
{
}