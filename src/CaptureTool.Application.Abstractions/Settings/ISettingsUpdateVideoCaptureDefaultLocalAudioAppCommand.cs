using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Application.Abstractions.Settings;

public interface ISettingsUpdateVideoCaptureDefaultLocalAudioAppCommand : IAsyncAppCommand<bool>
{
}
