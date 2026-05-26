using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Application.Abstractions.VideoEdit;

public interface ISaveVideoFileAppCommand : IAsyncAppCommand<string>
{
}
