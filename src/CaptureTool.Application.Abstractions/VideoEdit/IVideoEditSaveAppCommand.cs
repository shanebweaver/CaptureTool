using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Application.Abstractions.VideoEdit;

public interface IVideoEditSaveAppCommand : IAsyncAppCommand<string>
{
}
