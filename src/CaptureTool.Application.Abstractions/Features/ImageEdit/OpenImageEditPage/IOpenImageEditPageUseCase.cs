using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.ImageEdit.OpenImageEditPage;

public interface IOpenImageEditPageUseCase : IUseCase<OpenImageEditPageRequest, OpenImageEditPageResponse>, IConditional<OpenImageEditPageRequest>
{
}