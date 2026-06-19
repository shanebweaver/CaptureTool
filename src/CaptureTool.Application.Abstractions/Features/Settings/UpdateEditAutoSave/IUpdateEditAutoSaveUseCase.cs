using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Settings.UpdateEditAutoSave;

public interface IUpdateEditAutoSaveUseCase : IUseCase<UpdateEditAutoSaveRequest, UpdateEditAutoSaveResponse>, IConditional<UpdateEditAutoSaveRequest>
{
}
