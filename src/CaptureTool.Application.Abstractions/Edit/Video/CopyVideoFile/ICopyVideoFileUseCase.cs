using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Edit.Video.CopyVideoFile;

public interface ICopyVideoFileUseCase : IUseCase<CopyVideoFileRequest, CopyVideoFileResponse>, IConditional<CopyVideoFileRequest>
{
}