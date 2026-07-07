using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Settings.UpdateImageAutoSave;

public interface IUpdateImageAutoSaveUseCase : IUseCase<UpdateImageAutoSaveRequest, UpdateImageAutoSaveResponse>, IConditional<UpdateImageAutoSaveRequest>
{
}