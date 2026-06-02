using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.VideoEdit.CopyVideoFile;

public interface ICopyVideoFileUseCase : IUseCase<CopyVideoFileRequest, CopyVideoFileResponse>, IConditional<CopyVideoFileRequest>
{
}