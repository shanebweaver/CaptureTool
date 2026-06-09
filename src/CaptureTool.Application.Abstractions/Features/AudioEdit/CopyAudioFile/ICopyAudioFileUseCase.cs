using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.AudioEdit.CopyAudioFile;

public interface ICopyAudioFileUseCase : IUseCase<CopyAudioFileRequest, CopyAudioFileResponse>, IConditional<CopyAudioFileRequest>
{
}