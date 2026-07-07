using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Edit.Audio.CopyAudioFile;

public interface ICopyAudioFileUseCase : IUseCase<CopyAudioFileRequest, CopyAudioFileResponse>, IConditional<CopyAudioFileRequest>
{
}