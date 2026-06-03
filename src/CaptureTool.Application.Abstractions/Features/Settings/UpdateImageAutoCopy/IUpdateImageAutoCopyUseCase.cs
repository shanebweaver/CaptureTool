using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Settings.UpdateImageAutoCopy;

public interface IUpdateImageAutoCopyUseCase : IUseCase<UpdateImageAutoCopyRequest, UpdateImageAutoCopyResponse>, IConditional<UpdateImageAutoCopyRequest>
{
}