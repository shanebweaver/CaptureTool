using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Edit.Video.SaveVideoFile;

public interface ISaveVideoFileUseCase : IUseCase<SaveVideoFileRequest, SaveVideoFileResponse>, IConditional<SaveVideoFileRequest>
{
}