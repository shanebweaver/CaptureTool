using CaptureTool.Infrastructure.Abstractions.Commands;
using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.Abstractions.ImageEdit;

public interface IOpenImageEditPageAppCommand : IAppCommand<IImageFile>
{
}
