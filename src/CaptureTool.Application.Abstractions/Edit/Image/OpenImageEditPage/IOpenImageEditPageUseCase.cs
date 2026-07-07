using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Edit.Image.OpenImageEditPage;

public interface IOpenImageEditPageUseCase : IUseCase<OpenImageEditPageRequest, OpenImageEditPageResponse>, IConditional<OpenImageEditPageRequest>
{
}