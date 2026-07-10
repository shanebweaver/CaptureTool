using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Edit.External;

public interface IOpenExternalEditorUseCase : IUseCase<OpenExternalEditorRequest, OpenExternalEditorResponse>, IConditional<OpenExternalEditorRequest>
{
}
