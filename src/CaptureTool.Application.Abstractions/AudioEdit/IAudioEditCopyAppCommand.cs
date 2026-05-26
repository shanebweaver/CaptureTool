using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Application.Abstractions.AudioEdit;

public interface IAudioEditCopyAppCommand : IAsyncAppCommand<string>
{
}