using CaptureTool.Infrastructure.Abstractions.Commands;
using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.Abstractions.VideoEdit;

public interface IOpenVideoEditPageAppCommand : IAppCommand<IVideoFile>
{
}