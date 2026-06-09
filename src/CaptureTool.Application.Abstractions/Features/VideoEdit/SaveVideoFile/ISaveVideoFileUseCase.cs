using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.VideoEdit.SaveVideoFile;

public interface ISaveVideoFileUseCase : IUseCase<SaveVideoFileRequest, SaveVideoFileResponse>, IConditional<SaveVideoFileRequest>
{
}