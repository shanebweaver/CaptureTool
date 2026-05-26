using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Application.Abstractions.Settings;

public interface ISettingsUpdateVideoCaptureAutoCopyAppCommand : IAsyncAppCommand<bool>
{
}
